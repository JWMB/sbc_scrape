using CommonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NodaTime;
using sbc_scrape;
using sbc_scrape.SBC;
using SIE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scrape.Main.Tests
{
	[TestClass]
	public class SIEJoinTests
	{
		[TestMethod]
		public void GenerateCombos()
		{
			var list = new[] { 1, 2, 3, 4 };
			var combos = string.Join(" ", Combinatorics.GenerateCombos(list, 1).Select(o => string.Join(",", o)));
			Assert.AreEqual(combos, "1 2 3 4");

			combos = string.Join(" ", Combinatorics.GenerateCombos(list, 2).Select(o => string.Join(",", o)));
			Assert.AreEqual(combos, "1,2 1,3 1,4 2,3 2,4 3,4");

			combos = string.Join(" ", Combinatorics.GenerateCombos(list, 4).Select(o => string.Join(",", o)));
			Assert.AreEqual(combos, "1,2,3,4");
		}

		[TestMethod]
		public void NodaTests()
		{
			var d1 = new LocalDate(2000, 1, 1);
			var d2 = new LocalDate(2000, 2, 2);
			var p = Period.Between(d1, d2, PeriodUnits.Days);
			Assert.IsTrue(p.Days == 32);
		}

		[TestMethod]
		public async Task MatchSbcHtmlAndSIE()
		{
			var year = 2020;
			var roots = await SBCExtensions.ReadSIEFiles(new[] { "output_20201209.se" }.Select(file => Tools.GetOutputFolder("SIE", file)),  SBCExtensions.ProcessCompanyNameMode.SeparateIdAndName);
			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var resultRecords = roots.SelectMany(o => o.Children).OfType<ResultRecord>();

			var accountChanged = allVouchers.Where(o => o.Transactions.Where(
				t => t.AccountId >= 40000 && t.AccountId < 70000 && t.Amount != 0).GroupBy(t => Math.Sign(t.Amount)).Count() > 1).ToList();

			// From here, ignore AV
			allVouchers = allVouchers.Where(o => o.VoucherType != VoucherType.AV);
			//var byType = allVouchers.GroupBy(v => v.VoucherTypeCode).ToDictionary(g => g.Key, g => g.ToList());

			var htmlFolder = Tools.GetOutputFolder("sbc_html");
			var transactions = new BankTransactionSource().ReadAll(htmlFolder).Where(r => r.AccountingDate.Year == year).ToList();

			bool IsIncomeAccount(int accountId) => accountId >= 30110 && accountId <= 32910;

			// BGINB
			// AG
			// IT-A06 - 16899 OBS Konto?
			// KI - 16410 "Skattefordran"?
			var bankgiroSum = transactions.Where(o => o.Amount > 0 && o.Text.Any()).Sum(o => o.Amount); // o.Reference.EndsWith("BGINB") || o.Reference.EndsWith(" AG"))
			var incomes = allVouchers.SelectMany(o => o.Transactions.Where(t => IsIncomeAccount(t.AccountId)));
			var incomesSum = incomes.Sum(o => o.Amount);
			var resIncomedSum = resultRecords.Where(o => IsIncomeAccount(o.AccountId)).Sum(o => o.Amount);

			//var soso = transactions.Where(o => o.Amount > 0).ToList();
			var unusedVouchers = allVouchers.ToList();
			var result = MatchTransactions(transactions, unusedVouchers, (0, 0));
			var matched = result.matched;
			transactions = result.unmatched;

			//var unmatchedTxStrings = matchTx.Where(o => !o.Item2.Any()).Select(o => o.Item1.ToString());
			//transactions = transactions.Where(tx => unmatchedTxStrings.Contains(tx.ToString())).ToList();

			result = MatchTransactions(transactions, unusedVouchers, (-3, 3));
			matched.AddRange(result.matched);
			transactions = result.unmatched;

			var withDate = transactions.Select(o => (LocalDate.FromDateTime(o.AccountingDate), $"TX\t{o}"))
				.Concat(unusedVouchers.Select(o => (o.Date, FullVoucher(o))))
				.OrderBy(o => o.Item1).ToList();

			string FullVoucher(VoucherRecord v)
			{
				return $"{v}\n\t\t" + string.Join("\n\t\t", v.Transactions.Select(o => $"{o.Amount} {o.CompanyName}"));
			}
			var dbg = string.Join("\n", withDate.Select(o => $"{o.Item1.ToSimpleDateString()}\t{o.Item2}"));

			var invoices = new InvoiceSource().ReadAll(htmlFolder).Where(r => r.RegisteredDate.Year == year).ToList();
			var receipts = new ReceiptsSource().ReadAll(htmlFolder).Where(r => r.Date.Year == year).ToList();

			var recInv = receipts.Select(receipt => {
				return new { Receipt = receipt, Invoices = invoices.Where(o => o.Amount == receipt.Amount && o.PaymentDate == receipt.Date).ToList() };
			}).ToList();

			var triedMatchedInvoices = invoices.Select(invoice => {
				var matchedVouchers = unusedVouchers
					.Where(o => o.Series == invoice.VerSeries)
					.Where(o => o.SerialNumber == invoice.VerNum)
					//.Where(o => o.GetTransactionsMaxAmount() == invoice.Amount && o.Date == NodaTime.LocalDate.FromDateTime(invoice.RegisteredDate))
					//.Where(o => o.CompanyName == invoice.Supplier)
					.ToList();
				if (matchedVouchers.Count() == 1)
					RemoveVouchers(matchedVouchers, unusedVouchers);

				return new { Invoice = invoice, Vouchers = matchedVouchers };
			}).ToList();

			var matchedInvoices = triedMatchedInvoices.Where(o => o.Vouchers.Count == 1).Select(o => new { o.Invoice, Voucher = o.Vouchers.Single() }).ToList();
			var mismatch = triedMatchedInvoices.Where(o => o.Vouchers.Count != 1).ToList();

			var matchedReceipts = MatchReceipts(receipts, unusedVouchers);

			var doubleUse = matchedReceipts.SelectMany(o => o.Item2).GroupBy(o => o.Id).Where(o => o.Count() > 1).ToList();
			//var sieDir = Path.Join(Tools.GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			//var tmp = File.ReadAllText(Path.Combine(sieDir, "accountsexport.txt"));
		}

		(List<(BankTransaction, List<VoucherRecord>)> matched, List<BankTransaction> unmatched) MatchTransactions(List<BankTransaction> transactions, List<VoucherRecord> vouchers, (int daysBefore, int daysAfter) dateRange)
		{
			bool WithinRange(LocalDate d1, LocalDate d2)
			{
				var days = Period.Between(d1, d2, PeriodUnits.Days).Days;
				//var days = Period.Between(d1, d2).ToDuration().TotalDays;
				//var days = d2.Minus(d1).ToDuration().TotalDays;
				return days >= dateRange.daysBefore && days <= dateRange.daysAfter;
			}

			var matched = new List<(BankTransaction, List<VoucherRecord>)>();
			var unmatched = new List<BankTransaction>();
			var rxDigitsWithSpace = new Regex(@"^(\d+)\s(\d+)$");
			foreach (var tx in transactions)
			{
				var exactMatch = new List<VoucherRecord>();
				var accountingDate = NodaTime.LocalDate.FromDateTime(tx.AccountingDate);
				var inDateRange = vouchers.Where(o => WithinRange(accountingDate, o.Date)).ToList();
				if (tx.Text == "LB UTTAG")
				{
					var lbs = inDateRange.Where(o => o.VoucherType == VoucherType.LB).ToList();
					exactMatch = FindComboWithExactAmountMatch(lbs, -tx.Amount);
					RemoveVouchers(exactMatch, vouchers);
				}
				else
				{
					var m = rxDigitsWithSpace.Match(tx.Text);
					if (m.Success)
					{
						var fas = inDateRange.Where(o => o.VoucherType == VoucherType.FAS).ToList();
						exactMatch = FindComboWithExactAmountMatch(fas, tx.Amount);
						RemoveVouchers(exactMatch, vouchers);
					}
					else
					{
						exactMatch = FindComboWithExactAmountMatch(inDateRange, tx.Amount);
						RemoveVouchers(exactMatch, vouchers);
					}
				}
				if (exactMatch.Any())
					matched.Add((tx, exactMatch));
				else
					unmatched.Add(tx);
			}
			return (matched, unmatched);
		}

		//private static int numTimesCalled = 0;
		List<VoucherRecord> FindComboWithExactAmountMatch(IList<VoucherRecord> vouchers, decimal matchAmount)
		{
			var foundExact = vouchers.FirstOrDefault(o => o.GetTransactionsMaxAmount() == matchAmount);
			if (foundExact != null)
				return new List<VoucherRecord> { foundExact };

			if (vouchers.Count > 20) //combinatorics will take too long
				return new List<VoucherRecord>();

			var amounts = vouchers.Select(o => o.GetTransactionsMaxAmount()).ToList();
			for (int numItems = 2; numItems <= vouchers.Count; numItems++)
			{
				var match = Combinatorics.GenerateCombos(amounts, numItems).FirstOrDefault(list => list.Sum() == matchAmount);
				if (match != null)
				{
					var vouchersCopy = new List<VoucherRecord>(vouchers);
					var result = new List<VoucherRecord>();
					foreach (var amount in match)
					{
						var index = vouchersCopy.FindIndex(v => v.GetTransactionsMaxAmount() == amount);
						result.Add(vouchersCopy[index]);
						vouchersCopy.RemoveAt(index);
					}
					return result;
				}
			}
			return new List<VoucherRecord>();
		}

		void RemoveVouchers(IEnumerable<VoucherRecord> vs, List<VoucherRecord> from)
		{
			var removeIds = vs.Select(o => o.Id).ToList();
			from.RemoveAll(v => removeIds.Contains(v.Id));
		}

		List<(Receipt, List<VoucherRecord>)> MatchReceipts(IEnumerable<Receipt> receipts, List<VoucherRecord> vouchers)
		{
			return receipts.Select(receipt => {
				var matchedVouchers = vouchers
					.Where(o => o.GetTransactionsMaxAmount() == receipt.Amount && o.Date == NodaTime.LocalDate.FromDateTime(receipt.Date))
					.ToList();
				if (matchedVouchers.Count() > 1)
				{
					var tmp = matchedVouchers.Where(o => o.CompanyName == receipt.Supplier).ToList();
					if (tmp.Count() == 1)
						matchedVouchers = tmp;
					else
					{
						var tmp2 = matchedVouchers.Where(o => o.Transactions.First().CompanyId.ToString().EndsWith(receipt.SupplierId)).ToList();
						if (tmp2.Count() == 1)
							matchedVouchers = tmp2;
						else
						{

						}
					}
					//Debug.WriteLine(tmp);
				}
				if (matchedVouchers.Count == 1)
					RemoveVouchers(matchedVouchers, vouchers);
				return (receipt, matchedVouchers);
			}).ToList();
		}
	}
}
