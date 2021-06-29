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
		//public async Task MatchMediusFlowWithSIE__X()
		//{
		//	var years = new[] { 2017, 2018, 2019, 2020 }; //
		//	var store = new FileSystemKVStore(Tools.GetOutputFolder());

		//	var invoiceStore = new SBCScan.InvoiceStore(store);
		//	var files = await invoiceStore.GetKeysParsed();
		//	var invoices = (await Task.WhenAll(files.Where(o => years.Contains(o.RegisteredDate.Year)).Select(async o => await invoiceStore.Get(o)).ToList())).ToList();


		//	var sieFolder = Tools.GetOutputFolder("SIE");
		//	var sieFiles = new DirectoryInfo(sieFolder).GetFiles($"output_*.se").Select(o => o.Name).Where(o => years.Any(p => o.Contains(p.ToString())));
		//	var sie = await SBCExtensions.ReadSIEFiles(sieFiles.Select(file => Tools.GetOutputFolder("SIE", file)));

		//	var dir = Tools.GetOutputFolder("sbc_html");
		//	var sbcInvoices = new SBC.InvoiceSource().ReadAll(dir).Where(o => years.Contains(o.RegisteredDate.Year));

		//	await MatchMediusFlowWithSIE(years, invoices, sie, sbcInvoices);
		//}

		private static (List<InvoiceSummary>, List<VoucherRecord>) PrepareFiltered(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie)
		{
			// SLRs company names are supposedly same as LB, but LRs can have
			var sieVouchers = sie.SelectMany(o => o.Children).OfType<VoucherRecord>().ToList();

			var invoiceSummaries = invoices.Where(o => o.IsRejected == false)
				.Select(o => InvoiceSummary.Summarize(o)).Where(o => years.Contains(o.InvoiceDate.Value.Year)).ToList();

			// TODO: hardcoded!!
			var companyAliases = new Dictionary<string, string> {
				{ "Sita Sverige AB", "SUEZ Recycling AB" },
				//{ "PreZero Recycling AB", "SUEZ Recycling AB" },
				{ "Fortum Värme", "Stockholm Exergi" },
				{ "Markservice STHLM AB", "Svensk Markservice AB" }
			};
			sieVouchers.Where(o => companyAliases.ContainsKey(o.CompanyName)).ToList().ForEach(o => o.SetCompanyNameOverride(companyAliases[o.CompanyName]));

			return (invoiceSummaries, sieVouchers);
		}

		public static List<MultiSourceRow> Testing(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie, List<SBC.Invoice> sbcInvoices)
		{
			var (invoiceSummaries, sieVouchers) = PrepareFiltered(years, invoices, sie);

			var slrs = sieVouchers.Where(voucher => voucher.VoucherType == VoucherType.SLR);
			// TODO: what about VoucherType.TaxAndExpense? // always without other references
			var lrs = sieVouchers.Where(voucher => voucher.VoucherType == VoucherType.TaxAndExpense).ToList();

			//var aaa = CreateSet(new[] { 1, 2, 3, 4, 5, 5 }, new[] { 0, 3, 4, 4, 5, 6, 7, 7 }, k => k, k => k);
			//var set = CreateSet(new[] { 1, 2, 3, 4, 5, 5 }, new[] { 0, 3, 4, 4, 5, 6, 7, 7 }, k => k, k => k);
			var slrToSbc = CreateSet(slrs, sbcInvoices, slr => slr.Id, sbc => sbc.IdSLR);
			if (slrToSbc.ManyA_To_ManyBs.Any(o => o.Item1.Count > 1))
				throw new Exception("Multi!");

			var missingSbcs = slrToSbc.OnlyInA;
			if (slrToSbc.OnlyInB.Any())
				throw new Exception("missingSlrs!");


			var lbs = sieVouchers.Where(voucher => voucher.VoucherType == VoucherType.LB).ToList();
			var ooo = CreateSet(lbs, sbcInvoices,
				lb => new InvoiceKey { Date = lb.Date, Amount = lb.GetTransactionsMaxAmount() },
				sbc => new InvoiceKey { Date = sbc.PaymentDate.Value.ToLocalDate(), Amount = sbc.Amount });
			// TODO: join with slrToSbc above

			var mfToJoined = CreateSet(invoiceSummaries, slrToSbc.OneA_To_NoneOrAnyBs,
				InvoiceKey.From,
				join1 => new InvoiceKey { Date = join1.A.Date, Amount = join1.A.GetTransactionsMaxAmount(), Company = join1.A.CompanyName });

			var paired = mfToJoined.OneA_To_NoneOrAnyBs;
			if (mfToJoined.OneA_To_ManyBs.Any())
				throw new Exception("OneA_To_ManyBs");
			if (mfToJoined.ManyA_To_ManyBs.Any())
			{
				if (mfToJoined.ManyA_To_ManyBs.Any(o => o.As.Count != o.Bs.Count))
					throw new Exception("Unsalvageble ManyA_To_ManyBs");
				// separate into pairs
				foreach (var item in mfToJoined.ManyA_To_ManyBs)
					for (int i = 0; i < item.As.Count; i++)
						paired.Add((item.As[i], item.Bs.Skip(i).Take(1).ToList()));
			}
			var retyped = paired.Select(o => new { A = o.A, B = o.Bs.SingleOrDefault() });

			var tmp = retyped.Select(o => {
				return new
				{
					SortDate = o.A.InvoiceDate.Value.ToLocalDate(),
					InvoiceDate = o.A.InvoiceDate.ToLocalDate(),
					CompanyName = o.A.Supplier,
					Amount = o.A.GrossAmount,
					SlrId = o.B.A?.Id,
					MFId = o.A.Id,
					Link = string.Join("", o.B.Bs?.Select(s => s.InvoiceLink).Distinct() ?? new List<string>()),
					//SubCount = o.B.Bs?.Count
				};
			}).Concat(mfToJoined.OnlyInB.Select(o => {
				return new
				{
					SortDate = o.A.Date,
					InvoiceDate = (LocalDate?)o.A.Date,
					CompanyName = o.A.CompanyName,
					Amount = o.A.GetTransactionsMaxAmount(),
					SlrId = o.A.Id,
					MFId = 0L,
					Link = string.Join("", o.Bs?.Select(s => s.InvoiceLink).Distinct() ?? new List<string>()),
					//SubCount = (int?)o.Bs.Count
				};
			}));

			var result = tmp.OrderByDescending(o => o.SortDate)
				.ThenBy(o => o.CompanyName)
				.ThenBy(o => o.Amount).ToList();

			var csv = string.Join("\n", result.Select(row => string.Join("\t", new[] {
				DateToString(row.SortDate),
				row.Amount.ToString("#"),
				//row.SourceType,
				row.CompanyName,
				DateToString(row.InvoiceDate),
				row.SlrId?.ToString(),
				row.MFId.ToString(),
				row.Link.ToString()
				//DateToString(row.DueDate),
				//DateToString(row.PayDate),
				//row.Id
			})));

			return null;
		}
		private class InvoiceKey
		{
			public LocalDate Date { get; set; }
			public decimal Amount { get; set; }
			public string Company { get; set; }
			public static InvoiceKey From(InvoiceSummary inv) => new InvoiceKey { Date = inv.InvoiceDate.ToLocalDate().Value, Amount = inv.GrossAmount, Company = inv.Supplier };

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
		}

		public class Set<TA, TB, TCompare>
		{
			public List<(TA A, TB B)> OneA_To_OneB => joined.Values
				.Where(o => o.Count() == 1 && o.Single().Outer?.Count == 1)
				.Select(o => (o.Single().Inner, o.Single().Outer.Single() ))
				.ToList();

			public List<(TA A, List<TB> Bs)> OneA_To_NoneOrAnyBs => joined.Values
				.Where(o => o.Count() == 1)
				.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
				.ToList();

			public List<(TA A, List<TB> Bs)> OneA_To_AnyBs => joined.Values
				.Where(o => o.Count() == 1)
				.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
				.Where(o => o.Item2.Any())
				.ToList();

			public List<(TA A, List<TB> Bs)> OneA_To_ManyBs => joined.Values
				.Where(o => o.Count() == 1)
				.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
				.Where(o => o.Item2.Count > 1)
				.ToList();

			public List<(List<TA> As, List<TB> Bs)> ManyA_To_ManyBs => joined.Values
				.Where(o => o.Count() > 1 || o.Any(p => p.Outer?.Count > 1))
				.Select(o => (o.Select(p => p.Inner).ToList(), outerByKey.GetValueOrDefault(o.First().Key, new List<TB>()).ToList()))
				.Where(o => o.Item1.Count > 1 && o.Item2.Count > 1)
				.ToList();

			public List<TA> OnlyInA => joined.Values
				.Where(o => o.Count() == 1 && o.Single().Outer?.Any() != true)
				.Select(o => o.Single().Inner)
				.ToList();

			public List<TB> OnlyInB => outerByKey
				.Where(o => !joined.ContainsKey(o.Key))
				.SelectMany(o => o.Value).ToList();

			private Dictionary<TCompare, List<Join>> joined;
			private Dictionary<TCompare, List<TB>> outerByKey;

			private class Join
			{
				public TCompare Key { get; set; }
				public TA Inner { get; set; }
				public List<TB> Outer { get; set; }
			}

			public static Set<TA, TB, TCompare> Create(IEnumerable<TA> a, IEnumerable<TB> b,
				Func<TA, TCompare> keyA, Func<TB, TCompare> keyB)
			{
				var outerByKey = b.GroupBy(o => keyB(o)).ToDictionary(o => o.Key, o => o.ToList());
				var joined = a.Select(o => {
					var key = keyA(o);
					var found = outerByKey.GetValueOrDefault(key, null);
					return new Join { Key = key, Inner = o, Outer = found };
				})
					.GroupBy(o => o.Key).ToDictionary(o => o.Key, o => o.ToList());
				var tmp = new Set<TA, TB, TCompare>();
				tmp.joined = joined;
				tmp.outerByKey = outerByKey;
				return tmp;
			}
		}
		public static Set<TA, TB, TCompare> CreateSet<TA, TB, TCompare>(IEnumerable<TA> a, IEnumerable<TB> b,
				Func<TA, TCompare> keyA, Func<TB, TCompare> keyB)
		{
			return Set<TA, TB, TCompare>.Create(a, b, keyA, keyB);
		}

		//public static (List<(TInner, TOuter)>, List<TInner>, List<TOuter>) GroupJoinOuters<TInner, TOuter, TCompare>(IEnumerable<TInner> inner, IEnumerable<TOuter> outer,
		//	Func<TInner, TCompare> keyInner, Func<TOuter, TCompare> keyOuter)
		//{
		//	var groupedByInner = inner.GroupJoin(outer, keyInner, keyOuter, (inner, outer) => new { Inner = inner, Outer = outer.ToList() }).ToList();
		//	var matched1to1 = groupedByInner.Where(o => o.Outer.Count == 1).Select(o => new { Inner = o.Inner, Outer = o.Outer.First() }).ToList();
		//	var multiMatch = groupedByInner.Where(o => o.Outer.Count > 1);
		//	var noMatchWithOuter = groupedByInner.Where(o => !o.Outer.Any()).Select(o => o.Inner).ToList();
		//	var matchedOuterKeys = groupedByInner.SelectMany(o => o.Outer).Select(keyOuter).ToList();
		//	var noMatchWithInner = outer.Where(o => matchedOuterKeys.Contains(keyOuter(o))).ToList();

		//	return (matched1to1.Select(o => (o.Inner, o.Outer)).ToList(), noMatchWithOuter, noMatchWithInner);
		//}

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
			string ShortenComments(string comments)
			{
				if (string.IsNullOrWhiteSpace(comments?.Trim()))
					return comments;

				var namePattern = @"(?<firstname>[\w]+)(?<middlenames>\s[\w]+){0,}(?<lastname>\s[\w]+)";
				var idPattern = @"(?<pid>\s?\(\d+\)\s*)(?!\(\d{2}-\d{2})";
				return new Regex(namePattern + idPattern).Replace(comments, "${firstname}${lastname}");
				// First Middle Middle Last (1234567) (12-18 10:51):Projektadministration / konsultation,First Last (1234568) (12-23 13:57):Möte med anbudsgivare - OK!
				//var rx = new Regex(@"(?<pid>\s?\(\d+\)\s*)(?!\(\d{2}-\d{2})");
				//return rx.Replace(comments, "");
			}
			var rows = //string.Join("\n", new[] { header }.Concat(
				fullResult.OrderByDescending(o => o.RegisteredDate)
				.Select(o =>
				{
					var supplier = o.SLR.Transactions.First().CompanyName;
					var comments = o.Invoice != null ? ShortenComments(o.Invoice?.Comments ?? "")
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
