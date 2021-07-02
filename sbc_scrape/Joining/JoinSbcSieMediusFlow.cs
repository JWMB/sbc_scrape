using MediusFlowAPI;
using NodaTime;
using Scrape.IO.Storage;
using SIE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace sbc_scrape.Joining
{
	public class JoinSbcSieMediusFlow
	{
		private static (List<InvoiceSummary>, List<VoucherRecord>) PrepareFiltered(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie)
		{
			// SLRs company names are supposedly same as LB, but LRs can have
			var sieVouchers = sie.SelectMany(o => o.Children).OfType<VoucherRecord>().ToList();

			var invoiceSummaries = invoices.Where(o => o.IsRejected == false)
				.Select(o => InvoiceSummary.Summarize(o)).Where(o => years.Contains(o.InvoiceDate.Value.Year)).ToList();

			//var companyAliases = new Dictionary<string, string> {
			//	{ "Sita Sverige AB", "SUEZ Recycling AB" },
			//	//{ "PreZero Recycling AB", "SUEZ Recycling AB" },
			//	{ "Fortum Värme", "Stockholm Exergi" },
			//	{ "Markservice STHLM AB", "Svensk Markservice AB" }
			//};
			//sieVouchers.Where(o => companyAliases.ContainsKey(o.CompanyName)).ToList().ForEach(o => o.SetCompanyNameOverride(companyAliases[o.CompanyName]));

			return (invoiceSummaries, sieVouchers);
		}

		class NameNormalizer
		{
			private Dictionary<string, string> lookUp;
			public NameNormalizer()
			{
				// TODO: hardcoded!!
				var replacements = new Dictionary<string, List<string>> {
					{ "Markservice STHLM AB", new List<string>{ "Svensk Markservice AB" } },
					{ "SUEZ Recycling AB", new List<string>{ "PreZero Recycling AB", "Sita Sverige AB" } },
					{ "Stockholm Exergi", new List<string> { "Fortum Värme" } },
					{ "Ellevio AB", new List<string> { "Energikundservice Sverige AB" } },
					{ "Handelsbanken", new List<string>{ "Stadshypotek AB" } },
					{ "Stockholms Markkontor", new List<string>{ "Expoateringskontoret Stockholms Markkontor" } },
					{ "Byggrevision Fastighet i Stockholm AB", new List<string>{ "Byggrevision Fastighet i Stock" } },
					{ "Hyresgästföreningen Region Stockholm", new List<string>{ "Hyresgästföreningen Region Sto" } },
					{ "iZettle AB - Fakturabetalningar", new List<string>{ "iZettle AB - Fakturabetalninga" } },
					{ "Beatrice Fejde Revisionsbyrå AB", new List<string>{ "Beatrice Fejde Revisionsbyrå A" } },
					{ "Larm & Passerkontroll i Stockholm AB", new List<string>{ "Larm & Passerkontroll i Stockh" } },
					{ "Skärholmens Bil & Trakt", new List<string>{ "Skärholmens Bil & Trak" } },
				};
				lookUp = replacements.SelectMany(o => o.Value.Select(p => new { Key = p, Value = o.Key }))
					.ToDictionary(o => o.Key, o => o.Value);
			}
			public string Normalize(string name) => lookUp.GetValueOrDefault(name, name);
		}

		public static List<MultiSourceRow> Testing(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie, List<SBC.Invoice> sbcInvoices, List<SBC.Receipt> receipts)
		{
			var (invoiceSummaries, sieVouchers) = PrepareFiltered(years, invoices, sie);

			// 1. Join SLR with SBC Invoices - the SBC always have an SLR Id
			// 2. Join LB with above result (they have the same Date as SBC PaymentDate and same SupplierId).
			// 3. Join MediusFlow with above result, using Date, Amount and (normalized) Company
			// 4. 

			// 1. Join SLR -> SBC
			var mainTransactionAccount = 24400;
			var slrs = sieVouchers.Where(voucher => voucher.VoucherType == VoucherType.SLR || voucher.VoucherType == VoucherType.TaxAndExpense);
			// Note: VoucherType.TaxAndExpense are always without other references
			{
				var missingMainAccount = slrs.Where(o => !o.Transactions.Any(p => p.AccountId == mainTransactionAccount));
				if (missingMainAccount.Any())
					throw new Exception($"Algorithm based on all SLR/LRs having {mainTransactionAccount}");
			}

			var tmp = slrs.Select(o => new { Item = o, Names = o.Transactions.Select(p => $"{p.CompanyId}{p.CompanyName}").Distinct().ToList() })
				.Where(o => o.Names.Count > 1).ToList();

			//var aaa = CreateSet(new[] { 1, 2, 3, 4, 5, 5 }, new[] { 0, 3, 4, 4, 5, 6, 7, 7 }, k => k, k => k);
			//var set = CreateSet(new[] { 1, 2, 3, 4, 5, 5 }, new[] { 0, 3, 4, 4, 5, 6, 7, 7 }, k => k, k => k);
			var slrToSbc = CreateSet(slrs, sbcInvoices, slr => slr.Id, sbc => sbc.IdSLR);
			if (slrToSbc.ManyA_To_ManyBs.Any(o => o.Item1.Count > 1))
				throw new Exception("SLR/SBC Multimatch");

			if (slrToSbc.OnlyInB.Any())
				throw new Exception("SBC without SLR");

			var result = slrToSbc.OneA_To_NoneOrAnyBs
				.Select(o => new Joined { SLR = o.A, SBC = o.Bs }).ToList();

			// 2. Join LB -> above result
			var lbs = sieVouchers.Where(voucher => voucher.VoucherType == VoucherType.LB).ToList();
			var lbToResult = CreateSet(lbs, result.Where(o => o.SBC.Any()),
				lb => new InvoiceKey { Amount = lb.GetAmountTransactionAccount(mainTransactionAccount), Company = lb.Transactions.First().CompanyId, Date = lb.Date },
				sbc => new InvoiceKey { Amount = sbc.SBC.First().Amount, Company = sbc.SBC.First().LevNr, Date = sbc.SBC.FirstOrDefault(o => o.PaymentDate != null)?.PaymentDate.Value.ToLocalDate() ?? LocalDate.MaxIsoValue });
			if (lbToResult.OneA_To_ManyBs.Any())
				throw new Exception("SBC/LB Multimatch");

			lbToResult.OneA_To_OneB.ForEach(o => o.B.LB = o.A);
			lbToResult.OnlyInA.ForEach(o => result.Add(new Joined { LB = o }));

			var soso = lbToResult.JoinedByKey.Where(o => o.Key.Amount == 148).ToList();
			// TODO: we have Storuman Energi AB -Shb Finans 148 kr 2020_SLR6297_292 2020_SLR6297_294 - but unmatched? We DO have LBs!?

			if (lbToResult.ManyA_To_ManyBs.Any())
			{
				if (lbToResult.ManyA_To_ManyBs.Any(o => o.As.Count != o.Bs.Count))
					throw new Exception("Unsalvageble ManyA_To_ManyBs");
				foreach (var item in lbToResult.ManyA_To_ManyBs)
					for (int i = 0; i < item.As.Count; i++)
						item.Bs[i].LB = item.As[i];
			}

			var replacer = new NameNormalizer();

			// Join MediusFlow -> above result
			var mfToResult = CreateSet(invoiceSummaries, result.Where(o => o.SLR != null),
				inv => new InvoiceKey { Date = inv.InvoiceDate.ToLocalDate().Value, Amount = inv.GrossAmount, Company = replacer.Normalize(inv.Supplier) },
				r => new InvoiceKey { Date = r.SLR.Date, Amount = -r.SLR.GetAmountTransactionAccount(mainTransactionAccount), Company = replacer.Normalize(r.SLR.CompanyName) });
			//r.SLR.GetTransactionsMaxAmount()
			if (mfToResult.OneA_To_ManyBs.Any())
				throw new Exception("OneA_To_ManyBs");

			mfToResult.OneA_To_OneB.ForEach(o => o.B.MF = o.A);
			mfToResult.OnlyInA.ForEach(o => result.Add(new Joined { MF = o }));

			if (mfToResult.ManyA_To_ManyBs.Any())
			{
				if (mfToResult.ManyA_To_ManyBs.Any(o => o.As.Count != o.Bs.Count))
					throw new Exception("Unsalvageble ManyA_To_ManyBs");
				// separate into pairs
				foreach (var item in mfToResult.ManyA_To_ManyBs)
					for (int i = 0; i < item.As.Count; i++)
						item.Bs[i].MF = item.As[i];
			}

			// Try to pair unmatched LB/SLR (no SBCInvoice was found that could provide link between them)
			// TODO: If there's a MediusFlow, we could use that to get Payment/Invoice dates which should match LB/SLR records
			// Ignore before/after plausible period
			var earliestSLR = result.Where(o => o.SLR != null).Min(o => o.SortDate);
			var latestLB = result.Where(o => o.LB != null).Max(o => o.SortDate);
			var fuzzyMatchSelection = result.Where(o => o.SortDate > earliestSLR && o.SortDate < latestLB).ToList();
			var noSLR = fuzzyMatchSelection.Where(o => o.SLR == null && o.LB != null).ToList();
			var noLB = fuzzyMatchSelection.Where(o => o.SLR != null && o.LB == null).ToList();

			var byAmountAndName = noSLR.Concat(noLB)
				.GroupBy(o => $"{replacer.Normalize(o.CompanyName)}/{o.Amount}")
				.Where(o => o.Count() >= 2)
				.ToDictionary(o => o.Key, o => o.ToList());

			foreach (var kv in byAmountAndName)
			{
				var withLb = kv.Value.Where(o => o.LB != null).ToList();
				var withSlr = kv.Value.Where(o => o.SLR != null).ToList();

				while (withLb.Any() && withSlr.Any())
				{
					var diffs = withLb.Select(o => new
					{
						WithLB = o,
						Diffs = withSlr.Select(p => new
						{
							WithSLR = p,
							Diff = Math.Abs(Period.Between(
									p.PaymentDate ?? p.InvoiceDate.Value.PlusDays(30), o.PaymentDate.Value, PeriodUnits.Days)
								.Days)
						}).OrderBy(p => p.Diff).ToList()
					}).ToList();
					var withMinDiff = diffs.OrderBy(o => o.Diffs.Select(p => p.Diff).Min()).First();
					var other = withMinDiff.Diffs.First().WithSLR;
					withMinDiff.WithLB.Merge(other);
					other.MakeEmpty();

					withLb.Remove(withMinDiff.WithLB);
					withSlr.Remove(other);
				}
			}
			result = result.Where(o => !o.IsEmpty()).ToList();

			{
				// Join with Receipts - very strange data from SBC though, same transaction may have MANY rows with different names?!
				var withLB = result.Where(o => o.LB != null).ToList();
				var keyToLB = withLB.GroupBy(o => new { CompanyId = o.LB.Transactions.First().CompanyId, Amount = o.LB.GetTransactionsMaxAmount() })
					.ToDictionary(o => o.Key, o => o.ToList());
				var groupedReceipts = receipts.GroupBy(o => new { Amount = o.Amount, Date = o.Date, SupplierId = o.SupplierId }).ToDictionary(o => o.Key, o => o.ToList());
				foreach (var groupedItem in groupedReceipts)
				{
					// SBC Admins often have strange chars in them, also numbers - probably not the name we want
					var rxBad1 = new Regex(@"[\/!\""]");
					var rxBad2 = new Regex(@"\d");
					var sorted = groupedItem.Value.OrderBy(o => (rxBad1.IsMatch(o.Supplier) ? 2 : 0) + (rxBad2.IsMatch(o.Supplier) ? 1 : 0)).ToList();
					var item = sorted.First();
					// seems LB CompanyId starts with some variation of BRF id (e.g. 0xxxx02 where xxxx is SIE CompanyIdRecord)
					var found = keyToLB.Where(o => o.Key.Amount == item.Amount && o.Key.CompanyId.EndsWith(item.SupplierId))
						.SelectMany(o => o.Value)
						.Where(o => !o.IsEmpty()) // we might have rendered it empty (below)
						.ToList();
					if (found.Count() > 1)
					{
						found = found.Select(o => new { Item = o, DateDiff = Math.Abs(Period.Between(o.LB.Date, item.Date.ToLocalDate()).Days) })
							.OrderBy(o => o.DateDiff).Select(o => o.Item).Take(1).ToList();
					}
					if (found.Any())
					{
						var first = found.First();
						first.Receipt = item;
						var potential = result.Where(r => r.Amount == item.Amount && r.SLR != null && r.LB == null && (r.SBC == null || !r.SBC.Any())).ToList();
						if (potential.Count == 1)
						{
							potential.Single().Merge(first);
							first.MakeEmpty();
						}
					}
				}
			}
			result = result.Where(o => !o.IsEmpty()).ToList();


			{
				// Split items with multiple SLR transactions into separate rows
				//Func<Joined, bool> predSLRMulti = o => o.SLR != null && o.SLR.TransactionsNonAdmin.Count() > 1;
				//Func<Joined, bool> predSBCMulti = o => o.SBC != null && o.SBC.Count() > 1;
				//var withMultiSLRTransactions = result.Where(o => predSLRMulti(o) || predSBCMulti(o)).ToList();
				//var notBoth = withMultiSLRTransactions.Where(o => !predSLRMulti(o) || !predSBCMulti(o)).ToList();
				var withMultiSLRTransactions = result.Where(o => o.SLR != null && o.SLR.TransactionsNonAdminOrCorrections.Count() > 1).ToList();
				foreach (var item in withMultiSLRTransactions)
				{
					var trans = item.SLR.TransactionsNonAdminOrCorrections.ToList();
					for (int i = 0; i < trans.Count; i++)
					{
						//var foundSBC = item.SBC?.Where(o => o.Amount == row.Amount).ToList();
						result.Add(new Joined
						{
							LB = item.LB,
							MF = item.MF,
							Receipt = item.Receipt,
							SLR = item.SLR,
							SBC = item.SBC,
							SLRTransactionRow = trans[i]
						});
					}
					result.Remove(item);
				}
			}

			var csv = ToCsv(result);
			return null;

			string ToCsv(IEnumerable<Joined> rows, bool sort = true)
			{
				if (sort)
				{
					rows = rows.OrderByDescending(o => o.SortDate)
						.ThenBy(o => o.CompanyName)
						.ThenBy(o => o.Amount);
				}
				return string.Join("\n", rows.Select(row => string.Join("\t", new[] {
					DateToString(row.SortDate),
					row.Amount.ToString("#"),
					row.CompanyName?.Replace("\"", "'"),
					row.AccountId.ToString(),
					DateToString(row.InvoiceDate),
					DateToString(row.PaymentDate),
					row.Comment,
					row.Link.ToString(),
					row.SLR?.Id.ToString(),
					row.MF?.Id.ToString(),
					row.LB?.Id,
					row.Missing,
				})));
			}
		}

		class Joined
		{
			public InvoiceSummary MF { get; set; }
			public VoucherRecord SLR { get; set; }
			public VoucherRecord LB { get; set; }
			public List<SBC.Invoice> SBC { get; set; }
			public SBC.Receipt Receipt { get; set; }

			public TransactionRecord SLRTransactionRow { get; set; }

			public void Merge(Joined other)
			{
				if (MF == null) MF = other.MF;
				if (SLR == null) SLR = other.SLR;
				if (LB == null) LB = other.LB;
				if (SBC == null || !SBC.Any()) SBC = other.SBC;
				if (Receipt == null) Receipt = other.Receipt;
			}
			public void MakeEmpty()
			{
				MF = null;
				SLR = null;
				LB = null;
				SBC = null;
				Receipt = null;
			}
			public bool IsEmpty() => MF == null && SLR == null && LB == null && (SBC == null || !SBC.Any());

			public string Missing => GetMissing(MF, SLR, LB, SBC);
			public LocalDate SortDate => InvoiceDate ?? LB.Date.PlusDays(-30);
			public LocalDate? InvoiceDate => MF?.InvoiceDate.Value.ToLocalDate() ?? SLR?.Date ?? SBC?.FirstOrDefault()?.RegisteredDate.ToLocalDate();
			public LocalDate? PaymentDate => LB?.Date ?? SBC?.FirstOrDefault()?.PaymentDate.ToLocalDate()
				?? new[] { MF?.FinalPostingingDate.ToLocalDate(), MF?.DueDate.ToLocalDate() }.Where(o => o != null).Max();

			public string CompanyName => (SLR?.VoucherTypeCode == "LR" ? Receipt?.Supplier : null) ?? MF?.Supplier ?? SLR?.CompanyName ?? SBC?.FirstOrDefault()?.Supplier ?? LB?.CompanyName ?? "";

			public decimal Amount => SLRTransactionRow?.Amount ?? MF?.GrossAmount ?? SLR?.GetTransactionsMaxAmount() ?? LB?.GetTransactionsMaxAmount()
				?? (SBC != null && SBC.Any() ? (decimal?)SBC.Sum(o => o.Amount) : null) ?? Receipt?.Amount ?? 0;

			public int? AccountId => SLRTransactionRow?.AccountId ?? (MF != null ? (int?)MF.AccountId : null) ?? SLR?.TransactionsNonAdminOrCorrections.FirstOrDefault()?.AccountId;

			public string Link => string.Join("", SBC?.Select(s => s.InvoiceLink).Distinct() ?? new List<string>());

			public string Comment => $"{((Receipt != null && SLR != null) ? SLR.CompanyName : "")} {InvoiceSummary.ShortenComments(MF?.Comments)}{(SLR != null && SLR.Transactions.Count > 1 ? string.Join(",", SLR.Transactions.Skip(1).Select(o => o.CompanyName).Distinct()) : "")}";

			public override string ToString() =>
				$"{DateToString(SortDate)} {Amount} {CompanyName} {Missing}";

			public static string GetMissing(InvoiceSummary MF, VoucherRecord SLR, VoucherRecord LB, List<SBC.Invoice> SBC) =>
	string.Join(",",
		new string[] { MF == null ? "M" : "", SLR == null ? "SLR" : "", LB == null ? "LB" : "", SBC == null || !SBC.Any() ? "SBC" : "" }
		.Where(o => o.Any())
	);
		}

		private class InvoiceKey
		{
			public LocalDate Date { get; set; }
			public decimal Amount { get; set; }
			public string Company { get; set; }

			//public static InvoiceKey From(InvoiceSummary inv) => new InvoiceKey { Date = inv.InvoiceDate.ToLocalDate().Value, Amount = inv.GrossAmount, Company = inv.Supplier };

			public override bool Equals(object obj)
			{
				return obj is InvoiceKey key &&
					   Date.Equals(key.Date) &&
					   Amount == key.Amount &&
					   Company == key.Company;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(Date, Amount, Company);
			}
			public override string ToString() => $"{Date} {Amount} {Company}";
		}

		public static Set<TA, TB, TCompare> CreateSet<TA, TB, TCompare>(IEnumerable<TA> a, IEnumerable<TB> b,
				Func<TA, TCompare> keyA, Func<TB, TCompare> keyB)
		{
			return Set<TA, TB, TCompare>.Create(a, b, keyA, keyB);
		}

		public static List<MultiSourceRow> Union(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie, List<SBC.Invoice> sbcInvoices)
		{
			var (invoiceSummaries, sieVouchers) = PrepareFiltered(years, invoices, sie);

			var result = new List<MultiSourceRow>()
				.Concat(invoiceSummaries
					.Select(o => new MultiSourceRow
					{
						InvoiceDate = o.InvoiceDate.ToLocalDate(),
						DueDate = o.DueDate.ToLocalDate(),
						PayDate = o.FinalPostingingDate.ToLocalDate(),
						CompanyName = o.Supplier,
						Amount = o.GrossAmount,
						SortDate = o.InvoiceDate.Value.ToLocalDate(),
						SourceType = "MediusFlow",
						Id = o.Id.ToString()
					}))
				.Concat(sieVouchers
					.Where(o => o.VoucherType == VoucherType.SLR || o.VoucherType == VoucherType.TaxAndExpense)
					.Select(o => new MultiSourceRow {
						InvoiceDate = o.Date,
						CompanyName = o.CompanyName,
						Amount = o.VoucherType == VoucherType.SLR ? o.TransactionsNonAdminOrCorrections.Sum(o => o.Amount) : o.GetTransactionsMaxAmount(),
						SortDate = o.Date, //.PlusDays(30),
						SourceType = o.VoucherTypeCode,
						Id = o.Id
					}))
				.Concat(sieVouchers
					.Where(o => o.VoucherType == VoucherType.LB)
					.Select(o => new MultiSourceRow
					{
						InvoiceDate = o.Date,
						CompanyName = o.CompanyName,
						Amount = o.GetTransactionsMaxAmount(),
						SortDate = o.Date.PlusDays(-30),
						SourceType = o.VoucherTypeCode,
						Id = o.Id
					}))

				.Concat(sbcInvoices
					.Where(o => years.Contains(o.RegisteredDate.Year))
					.Select(o => new MultiSourceRow
					{
						InvoiceDate = o.RegisteredDate.ToLocalDate(),
						PayDate = o.PaymentDate.ToLocalDate(),
						CompanyName = o.Supplier,
						Amount = o.Amount,
						SortDate = o.RegisteredDate.ToLocalDate(),
						SourceType = "SBC",
						Id = o.IdSLR // $"{o.VerSeries}/{o.VerNum}"
					}))
				.ToList();

			result = result.OrderByDescending(o => o.SortDate)
				.ThenBy(o => o.CompanyName)
				.ThenBy(o => o.Amount).ToList();
			var csv = string.Join("\n", result.Select(row => string.Join("\t", new[] {
				DateToString(row.SortDate),
				row.Amount.ToString("#"),
				row.SourceType,
				row.CompanyName,
				DateToString(row.InvoiceDate),
				DateToString(row.DueDate),
				DateToString(row.PayDate),
				row.Id
			})));
			return result;
		}

		private static string DateToString(LocalDate? date) => date?.ToSimpleDateString(); // .ToString("yyyy-MM-dd");

		public class MultiSourceRow
		{
			public LocalDate SortDate { get; set; } = new LocalDate();
			public LocalDate? InvoiceDate { get; set; }
			public LocalDate? DueDate { get; set; }
			public LocalDate? PayDate { get; set; }
			public string CompanyName { get; set; } = "";
			public decimal Amount { get; set; }
			public string SourceType { get; set; }
			public string Id { get; set; }
		}

		public static List<ExportRow> MatchMediusFlowWithSIE(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie, List<SBC.Invoice> sbcInvoices)
		{
			// SLR/LR are added when expense is registered, and contains the expense accounts
			// LB is when it's payed
			// LB will be dated at or after SLR/LR

			// SLRs company names are supposedly same as LB, but LRs can have
			var (invoiceSummaries, sieVouchers) = PrepareFiltered(years, invoices, sie);
			var result = InvoiceSIEMatch.MatchInvoiceWithLB_SLR(sieVouchers, invoiceSummaries);

			var missingPayment = result.Matches.Where(o => o.SLR.Voucher == null).ToList();
			if (missingPayment.Any())
			{
				// Ignore those with due date later than latest found payment:
				var latestDate = sieVouchers.Where(o => o.VoucherType == VoucherType.SLR).Max(o => o.Date).ToDateTimeUnspecified();
				missingPayment = missingPayment.Where(o => o.Invoice.DueDate < latestDate).ToList();
				if (missingPayment.Any())
				{
					System.Diagnostics.Debugger.Break();
				}
			}
			//Assert.IsFalse(missing.Any());

			// Some urgent invoices go (by request) direct to payment, without passing MediusFlow (e.g. 2020 "Office for design", "Stenbolaget")
			// The same goes for SBCs own periodical invoices and bank/interest payments
			// These should be found here: https://varbrf.sbc.se/Portalen/Ekonomi/Utforda-betalningar/
			// Actually, this is just a view for SLR records - with an additional link for the invoice

			// try getting LBs for sbcInvoices
			// tricky thing with sbcInvoices - one row for each non-admin transaction in SLRs
			var lbs = sieVouchers.Where(o => o.VoucherType == VoucherType.LB).ToList();
			var sbcInvoiceToLB = sbcInvoices.Where(o => o.PaymentDate != null).GroupBy(o => o.IdSLR)
				.Select(grp =>
				{
					var invoiceSum = grp.Sum(v => v.Amount);
					var paymentDate = LocalDate.FromDateTime(grp.First().PaymentDate.Value);
					return new
					{
						Invoice = grp.First(),
						LBs = lbs.Where(l => l.Date == paymentDate && l.Transactions.First(t => t.AccountId == 24400).Amount == invoiceSum).ToList()
					};
				});
			var sbcInvoiceToSingleLB = sbcInvoiceToLB.Where(o => o.LBs.Count == 1).ToDictionary(o => o.Invoice.IdSLR, o => o.LBs.Single());
			var stillUnmatchedLBs = result.UnmatchedLB.Except(sbcInvoiceToSingleLB.Values).ToList();

			var sbcMatchedSLRSNs = sbcInvoiceToSingleLB.Select(o => o.Key).ToList();
			var stillUnmatchedSLRs = result.UnmatchedSLR.Where(o => !sbcMatchedSLRSNs.Contains(o.Id));

			var vouchersSlrLr = sieVouchers.Where(o => o.VoucherType == VoucherType.TaxAndExpense).Concat(stillUnmatchedSLRs).ToList();
			var matchRemaining = MatchVoucherTypes(vouchersSlrLr, stillUnmatchedLBs, false);
			var stillUnmatchedSlrLr = vouchersSlrLr.Except(matchRemaining.Select(o => o.Key)).ToList();
			stillUnmatchedLBs = stillUnmatchedLBs.Except(matchRemaining.Select(o => o.Value)).ToList();

			var finalMatch = MatchVoucherTypes(stillUnmatchedSlrLr, stillUnmatchedLBs, true);
			stillUnmatchedSlrLr = stillUnmatchedSlrLr.Except(finalMatch.Select(o => o.Key)).ToList();
			stillUnmatchedLBs = stillUnmatchedLBs.Except(finalMatch.Select(o => o.Value)).ToList();
			foreach (var kv in finalMatch)
				matchRemaining.Add(kv.Key, kv.Value);

			var matchesBySLR = result.Matches.Where(o => o.SLR.Voucher != null)
				.Select(m => new { Key = m.SLR.Voucher.Id, Value = m })
				.ToDictionary(o => o.Key, o => o.Value);
			var fullResult = sieVouchers.Where(o => o.VoucherType == VoucherType.SLR || o.VoucherType == VoucherType.TaxAndExpense).Select(slr =>
			{
				// TODO: should we have multiple rows if SLR has >1 non-admin transaction?
				//var sbcInv = sbcInvoices.FirstOrDefault(o => o.RegisteredDate.Year == slr.Date.Year && o.VerNum == slr.SerialNumber);
				var sbcInv = sbcInvoices.FirstOrDefault(o => o.IdSLR == slr.Id);
				var info = new InvoiceInfo { SLR = slr, SbcImageLink = sbcInv?.InvoiceLink };
				if (matchesBySLR.TryGetValue(slr.Id, out var found))
				{
					info.Invoice = found.Invoice;
					info.LB = found.LB.Voucher;
				}
				if (info.LB == null && sbcInv != null)
				{
					if (sbcInvoiceToSingleLB.TryGetValue(sbcInv.IdSLR, out var lb))
						info.LB = lb;
				}
				if (info.LB == null && matchRemaining.ContainsKey(slr))
				{
					info.LB = matchRemaining[slr];
				}
				return info;
			}).OrderByDescending(o => o.RegisteredDate).ToList();

			fullResult.ForEach(o => { try { var tmp = o.MainAccountId; } catch { throw new Exception($"Missing MainAccountId in {o}"); } });

			var accountIdToAccountName = sie.SelectMany(s => s.Children.OfType<AccountRecord>()).GroupBy(o => o.AccountId).ToDictionary(o => o.Key, o => o.First().AccountName);
			foreach (var accountId in accountIdToAccountName.Keys)
			{
				var found = invoiceSummaries.FirstOrDefault(o => o.AccountId == accountId);
				if (found != null)
					accountIdToAccountName[accountId] = found.AccountName;
			}

			//var header = new[] {
			//	"Date",
			//	"Missing",
			//	"Amount",
			//	"Supplier",
			//	"AccountId",
			//	"AccountName",
			//	"Comments",
			//	"InvoiceId",
			//	"ReceiptId",
			//	"CurrencyDate",
			//	"TransactionText",
			//	"TransactionRef"
			//};
			var rows = //string.Join("\n", new[] { header }.Concat(
				fullResult.OrderByDescending(o => o.RegisteredDate)
				.Select(o =>
				{
					var supplier = o.SLR.Transactions.First().CompanyName;
					var comments = o.Invoice != null ? InvoiceSummary.ShortenComments(o.Invoice?.Comments ?? "")
							: (o.LB?.CompanyName != o.SLR.Transactions.First().CompanyName ? o.LB?.CompanyName : "");
					if (o.Invoice == null && !string.IsNullOrEmpty(comments))
					{
						// in this case SLR "CompanyName" is actually the purpose, and LB has recipient
						supplier = comments;
						comments = o.SLR.Transactions.First().CompanyName;
					}

					var accountChanged = o.SLR.Transactions.GroupBy(t => t.AccountId).Where(t => t.Select(tt => Math.Sign(tt.Amount)).Distinct().Count() > 1);
					return new ExportRow {
						Date = o.RegisteredDate,
						Missing = "",
						Amount = o.Amount,
						Supplier = supplier,
						AccountId = o.MainAccountId,
						AccountName = accountIdToAccountName[o.MainAccountId],
						Comments = comments,
						InvoiceId = o.Invoice?.Id.ToString() ?? "",
						ReceiptId = $"{o.SLR.Series} {o.SLR.SerialNumber}",
						CurrencyDate = o.LB?.Date,
						TransactionText = "",
						TransactionRef = string.IsNullOrEmpty(o.SbcImageLink) ? "" :
							(o.SbcImageLink.StartsWith("http") ? o.SbcImageLink : $"https://varbrf.sbc.se{o.SbcImageLink}"), //https://varbrf.sbc.se/InvoiceViewer.aspx?id=
						AccountChange = accountChanged.Any() ? string.Join(", ", accountChanged.Where(x => x.Key != o.MainAccountId).Select(x => x.Key).Distinct()) : "",
					};
				//	return new string[] {
				//		o.RegisteredDate.ToSimpleDateString(),
				//		"",
				//		o.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
				//		supplier,
				//		o.MainAccountId.ToString(),
				//		accountIdToAccountName[o.MainAccountId],
				//		comments,
				//		o.Invoice?.Id.ToString() ?? "",
				//		$"{o.SLR.Series} {o.SLR.SerialNumber}", //o.LB?.Id ?? "",
				//		o.LB?.Date.ToSimpleDateString() ?? "",
				//		"",
				//		string.IsNullOrEmpty(o.SbcImageLink) ? "" :
				//			(o.SbcImageLink.StartsWith("http") ? o.SbcImageLink : $"https://varbrf.sbc.se{o.SbcImageLink}"), //https://varbrf.sbc.se/InvoiceViewer.aspx?id=
				//		accountChanged.Any() ? string.Join(", ", accountChanged.Where(x => x.Key != o.MainAccountId).Select(x => x.Key).Distinct()) : "",
				//};
				}
				//)).Select(o => string.Join("\t", o))
			).ToList();
			return rows;
			// https://varbrf.sbc.se/Portalen/Ekonomi/Revisor/Underlagsparm/
		}

		private static Dictionary<VoucherRecord, VoucherRecord> MatchVoucherTypes(IEnumerable<VoucherRecord> vouchersA, IEnumerable<VoucherRecord> matchWithTheseB, bool ignoreCompanyName)
		{
			var matchWithTheseListB = matchWithTheseB.ToList();
			var vouchersListA = vouchersA.ToList();

			bool GetDateInRange(int days, int minInc, int maxEx) => days >= minInc && days < maxEx;
			var dateMatchPasses = new List<Func<LocalDate, LocalDate, bool>>
			{
				(voucherA, voucherB) => voucherA == voucherB,
				(voucherA, voucherB) => GetDateInRange(Period.Between(voucherA, voucherB, PeriodUnits.Days).Days, 0, 3),
				(voucherA, voucherB) => GetDateInRange(Period.Between(voucherA, voucherB, PeriodUnits.Days).Days, 0, 21),
				(voucherA, voucherB) => GetDateInRange(Period.Between(voucherA, voucherB, PeriodUnits.Days).Days, 0, 51),
			};
			if (!ignoreCompanyName)
			{
				dateMatchPasses.Add(
					(voucherA, voucherB) => GetDateInRange(Period.Between(voucherA, voucherB, PeriodUnits.Days).Days, 0, 91)
				);
			}

			var result = new Dictionary<VoucherRecord, VoucherRecord>();

			foreach (var dateMatch in dateMatchPasses)
			{
				var matches = vouchersListA.Select(vA =>
				{
				//if (ignoreCompanyName && vA.GetTransactionsMaxAmount() == 281)
				//{ }

				var lb_t24400 = vA.Transactions.First(t => t.AccountId == 24400);
					var foundLrs = matchWithTheseListB.Where(vB => dateMatch(vA.Date, vB.Date)).Where(l =>
					{
						var lr_t24400 = l.Transactions.First(t => t.AccountId == 24400);
						return lr_t24400.Amount == -lb_t24400.Amount
							&& (ignoreCompanyName ? true : lr_t24400.CompanyName == lb_t24400.CompanyName);
					});
					return new { A = vA, Bs = foundLrs.ToList() };
				});
				var goodMatches = matches.Where(o => o.Bs.Count() == 1).Select(o => (o.A, o.Bs.First())).ToList();
				matchWithTheseListB = matchWithTheseListB.Except(goodMatches.Select(o => o.Item2)).ToList();
				vouchersListA = vouchersListA.Except(goodMatches.Select(o => o.A)).ToList();

				goodMatches.ForEach(o => result.Add(o.A, o.Item2));
			}
			return result;
		}
	}

	public class ExportRow
	{
		public LocalDate Date { get; set; }
		public string Missing { get; set; }
		public decimal Amount { get; set; }
		public string Supplier { get; set; }
		public int AccountId { get; set; }
		public string AccountName { get; set; }
		public string Comments { get; set; }
		public string InvoiceId { get; set; }
		public string ReceiptId { get; set; }
		public LocalDate? CurrencyDate { get; set; }
		public string TransactionText { get; set; }
		public string TransactionRef { get; set; }
		public string AccountChange { get; set; }
	}

	public class InvoiceInfo
	{
		public VoucherRecord SLR { get; set; }
		public VoucherRecord? LB { get; set; }
		public InvoiceSummary? Invoice { get; set; }
		public string? SbcImageLink { get; set; }

		public int MainAccountId => SLR.Transactions
			.Where(t => SLR.VoucherType == VoucherType.SLR ? (t.AccountId > 30000 || t.AccountId == 23501) : t.AccountId != 24400)
			.GroupBy(t => t.AccountId)
			.Select(g => new { AccountId = g.Key, Sum = g.Sum(o => o.Amount) })
			.Where(o => o.Sum != 0).OrderByDescending(t => Math.Abs(t.Sum)).First().AccountId;
		public decimal Amount => -SLR.Transactions.First(t => t.AccountId == 24400).Amount; // ?? LB.Transactions.First(t => t.AccountId == 24400).Amount;
		public LocalDate RegisteredDate => SLR.Date;

		public override string ToString()
		{
			return $"{SLR.Date.ToSimpleDateString()} {SLR.CompanyName} {SLR.GetTransactionsMaxAmount()} {(Invoice == null ? "" : "INV")} {(SbcImageLink == null ? "" : "IMG")}";
		}
	}
}
