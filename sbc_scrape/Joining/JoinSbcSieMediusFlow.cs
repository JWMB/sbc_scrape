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

		public static List<ExportRow> MatchMediusFlowWithSIE(IEnumerable<int> years, List<InvoiceFull> invoices, List<RootRecord> sie, List<SBC.Invoice> sbcInvoices)
		{
			// SLR/LR are added when expense is registered, and contains the expense accounts
			// LB is when it's payed
			// LB will be dated at or after SLR/LR

			// SLRs company names are supposedly same as LB, but LRs can have
			invoices = invoices.Where(o => o.IsRejected == false).ToList();
			var vouchers = sie.SelectMany(o => o.Children).OfType<VoucherRecord>().ToList();

			var summaries = invoices.Select(o => InvoiceSummary.Summarize(o)).Where(o => years.Contains(o.InvoiceDate.Value.Year)).ToList();

			var companyAliases = new Dictionary<string, string> {
				{ "Sita Sverige AB", "SUEZ Recycling AB" },
				{ "Fortum Värme", "Stockholm Exergi" }
			};
			vouchers.Where(o => companyAliases.ContainsKey(o.CompanyName)).ToList().ForEach(o => o.SetCompanyNameOverride(companyAliases[o.CompanyName]));
			var result = InvoiceSIEMatch.MatchInvoiceWithLB_SLR(vouchers, summaries);

			var missing = result.Matches.Where(o => o.SLR.Voucher == null).ToList();
			if (missing.Any())
				System.Diagnostics.Debugger.Break();
			//Assert.IsFalse(missing.Any());

			// Some urgent invoices go (by request) direct to payment, without passing MediusFlow (e.g. 2020 "Office for design", "Stenbolaget")
			// The same goes for SBCs own periodical invoices and bank/interest payments
			// These should be found here: https://varbrf.sbc.se/Portalen/Ekonomi/Utforda-betalningar/
			// Actually, this is just a view for SLR records - with an additional link for the invoice

			// try getting LBs for sbcInvoices
			// tricky thing with sbcInvoices - one row for each non-admin transaction in SLRs
			var lbs = vouchers.Where(o => o.VoucherType == VoucherType.LB).ToList();
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

			var vouchersSlrLr = vouchers.Where(o => o.VoucherType == VoucherType.TaxAndExpense).Concat(stillUnmatchedSLRs).ToList();
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
			var fullResult = vouchers.Where(o => o.VoucherType == VoucherType.SLR || o.VoucherType == VoucherType.TaxAndExpense).Select(slr =>
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
				var found = summaries.FirstOrDefault(o => o.AccountId == accountId);
				if (found != null)
					accountIdToAccountName[accountId] = found.AccountName;
			}

			var header = new[] {
				"Date",
				"Missing",
				"Amount",
				"Supplier",
				"AccountId",
				"AccountName",
				"Comments",
				"InvoiceId",
				"ReceiptId",
				"CurrencyDate",
				"TransactionText",
				"TransactionRef"
			};
			string ShortenComments(string comments)
			{
				var rx = new Regex(@"(?<pid>\s?\(\d+\)\s*)(?!\(\d{2}-\d{2})");
				return rx.Replace(comments, "");
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
