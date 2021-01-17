using NodaTime;
using SIE.Matching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonTools;
using Xunit;

namespace SIE.Tests
{
	public class UnitTest1
	{
		[Fact]
		public void ParseLine()
		{
			var items = SIERecord.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

		[Fact]
		public void ParseAddress()
		{
			var items = SIERecord.ParseLine(@"#ADRESS ""SvenSvensson"" ""Box 21"" ""21120   MALM�"" ""040 - 12345""");
			var record = new AddressRecord();
			record.Read(items);

			Assert.Equal("040 - 12345", record.PhoneNumber);
		}

		[Fact]
		public async Task CheckIntegrity()
		{
			var year = 2020;
			var roots = await TestingTools.ReadSIEFiles(new[] { "output_20201209.se" });
			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var resultRecords = roots.SelectMany(o => o.Children).OfType<ResultRecord>();

			{
				// Unique Id for every voucher - NOTE currently just within a year!
				Assert.False(allVouchers.GroupBy(o => o.Id).Where(o => o.Count() > 1).Any());
			}

			{
				// Sum of all transactions within a voucher is 0
				var vouchersWithNonZeroSumTransactions = allVouchers.Select(o => new { Voucher = o, TxSum = o.Transactions.Sum(t => t.Amount) }).Where(o => o.TxSum != 0);
				Assert.False(vouchersWithNonZeroSumTransactions.Any());
			}

			{
				var slrs = allVouchers.Where(o => o.VoucherType == VoucherType.SLR);

				// All SLR should have transaction to 24400
				Assert.False(slrs.Where(o => o.Transactions.Any(t => t.AccountId == 24400) == false).Any());
			}
			{
				var lbs = allVouchers.Where(o => o.VoucherType == VoucherType.LB);

				// All SLR should have transaction to 24400
				Assert.False(lbs.Where(o => o.Transactions.Any(t => t.AccountId == 24400) == false).Any());
			}


			var balanceRecordsByAccount = roots.SelectMany(o => o.Children).OfType<BalanceRecord>().GroupBy(o => o.AccountId);
			var inOuts = balanceRecordsByAccount.Select(o => new
			{
				AccountId = o.Key,
				In = o.FirstOrDefault(p => p is IngoingBalanceRecord)?.Amount ?? 0,
				Out = o.FirstOrDefault(p => p is OutgoingBalanceRecord)?.Amount ?? 0
			});
			var balanceChanges = inOuts.Select(o => new { AccountId = o.AccountId, Change = o.Out - o.In }).ToDictionary(o => o.AccountId, o => o.Change);

			{
				// Sums of transactions per AccountId equals RES record values
				var summedTransactions = allVouchers.SelectMany(o => o.Transactions)
					.GroupBy(o => o.AccountId)
					.Select(g => new { AccountId = g.Key, Sum = g.Sum(o => o.Amount) })
					.OrderBy(g => g.AccountId);

				// IB/UB (Balance) records only include < 30000
				var balanceVsTransactions = summedTransactions.Where(o => o.AccountId < 30000)
					.Select(o => new { AccountId = o.AccountId, Diff = o.Sum - balanceChanges.GetValueOrDefault(o.AccountId, 0) }).ToList();
				Assert.DoesNotContain(balanceVsTransactions, o => o.Diff != 0);

				// RES records don't include accounts < 30000
				var summedRESTransactions = summedTransactions.Where(o => o.AccountId >= 30000);
				var diff = summedRESTransactions.GetListDiffs(o => o.AccountId, resultRecords, o => o.AccountId);
				// Any transactions with accountIds not present in RES should be 0 (probably incorrectly booked)
				Assert.DoesNotContain(diff.OnlyIn1, o => o.Sum != 0);
				// All RES record accounts are present in transactions
				Assert.False(diff.OnlyIn2.Any());

				var diffs = resultRecords.Select(rr => new { AccountId = rr.AccountId, Diff = rr.Amount - summedRESTransactions.First(v => v.AccountId == rr.AccountId).Sum });
				Assert.DoesNotContain(diffs, o => o.Diff != 0);
			}
		}

		[Fact]
		public async Task TestGetSalaries()
		{
			var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
			var roots = await TestingTools.ReadSIEFiles(files);
			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var ma = string.Join("\n", allVouchers.Where(o => o.VoucherTypeCode == "MA").OrderBy(o => o.Date).Select(o => o.ToHierarchicalString()));

			var withSalaries = allVouchers.Where(o => !(new[] { "LR", "BS", "MA", "AR" }.Contains(o.VoucherTypeCode))
				&& o.Transactions.Any(t => t.AccountId.ToString().StartsWith("647"))).OrderBy(o => o.Date); //27300
			var dbg = string.Join("\n", withSalaries.Select(o => o.ToHierarchicalString()));
			//var salaries = allVouchers.Where(o => o.VoucherType == VoucherType.Salary).OrderBy(o => o.Date).ToList();
		}

		[Fact]
		public async Task Experiment()
		{
			var root = (await TestingTools.ReadSIEFiles(new[] { "output_20201209.se" })).First();
			var byType = root.Children.OfType<VoucherRecord>().GroupBy(v => v.VoucherTypeCode).ToDictionary(g => g.Key, g => g.ToList());
		}


		[Fact]
		public async Task TestCreateMatched()
		{
			var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
			var roots = await TestingTools.ReadSIEFiles(files);
			var allAccountTypes = roots.SelectMany(o => o.Children).OfType<AccountRecord>().GroupBy(o => o.AccountId).ToDictionary(o => o.Key, o => string.Join(" | ", o.Select(o => o.AccountName).Distinct()));

			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var transactionsWithUndefinedAccounts = allVouchers.SelectMany(o => o.Transactions.Where(tx => !allAccountTypes.ContainsKey(tx.AccountId)).Select(tx => new { tx.CompanyName, tx.AccountId }));
			Assert.False(transactionsWithUndefinedAccounts.Any());

			var matchResult = MatchSLRResult.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);

			//All must have a single (24400|15200) transaction
			var transactionsMissingRequired = matchResult.Matches.SelectMany(o => new[] { o.SLR, o.Other })
				.Where(o => o.Transactions.Count(tx => TransactionMatched.RequiredAccountIds.Contains(tx.AccountId)) != 1);
			Assert.False(transactionsMissingRequired.Any());

			Assert.Equal(0, matchResult.Matches.Count(o => o.Other.TransactionsNonAdminOrCorrections.Count() > 1));

			var txs = TransactionMatched.FromVoucherMatches(matchResult, TransactionMatched.RequiredAccountIds);

			var dbg = string.Join("\n", txs.OrderBy(o => o.DateRegistered ?? LocalDate.MinIsoValue));
		}

		[Fact]
		public async Task Test1()
		{
			var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
			var roots = await TestingTools.ReadSIEFiles(files); // new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var annoyingAccountIds = new[] { 24400, 26410 }.ToList();
			IEnumerable<TransactionRecord> txFilter(IEnumerable<TransactionRecord> txs) =>
				TransactionRecord.PruneCorrections(txs).Where(t => !annoyingAccountIds.Contains(t.AccountId));

			var multi = allVouchers.Where(o => !(new[] { "FAS", "LAN", "LON", "MA", "BS", "RV" }.Contains(o.VoucherTypeCode)) &&
				TransactionRecord.PruneCorrections(o.Transactions).Count(t => !(new[] { '1', '2' }.Contains(t.AccountId.ToString()[0]))) > 1).ToList();

			var matchResult = MatchSLRResult.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);

			var multiAccount = matchResult.Matches.Where(mv => txFilter(mv.SLR.Transactions).Count() > 1);
			//var dbg2 = string.Join("\n\n", matchResult.Matched.OrderBy(o => o.other.Date)
			//	.Select(mv => $"{PrintVoucher(mv.slr, txFilter)}\n{PrintVoucher(mv.other, txFilter)}"));

			Assert.DoesNotContain(matchResult.Matches, o => !o.SLR.Transactions.Any());
			Assert.DoesNotContain(matchResult.NotMatchedOther, o => !o.Transactions.Any());
			Assert.DoesNotContain(matchResult.NotMatchedSLR, o => !o.Transactions.Any());

			var cc = matchResult.Matches
				.Select(o => new
				{
					o.Other.Date,
					txFilter(o.SLR.Transactions).First().Amount,
					txFilter(o.SLR.Transactions).First().AccountId,
					txFilter(o.SLR.Transactions).First().CompanyName,
					Comment = "",
				})
				.Concat((matchResult.NotMatchedSLR.Concat(matchResult.NotMatchedOther))
					.Select(o => new
					{
						o.Date,
						Amount = txFilter(o.Transactions).FirstOrDefault()?.Amount ?? 0,
						AccountId = 0,
						CompanyName = txFilter(o.Transactions).FirstOrDefault()?.CompanyName ?? "N/A",
						Comment = o.VoucherTypeCode,
					}));
			//cc = cc.Where(o => !string.IsNullOrEmpty(o.Comment) && o.Comment != "LON" && o.Comment != "LAN");
			cc = cc.OrderBy(o => o.Date).ToList();

			//var dbg = string.Join("\n", cc.Select(o => $"{o.Date.AtMidnight().ToDateTimeUnspecified():yyyy-MM-dd}\t{o.Comment}\t{o.Amount}\t{o.AccountId}\t{o.CompanyName}"));

			//string PrintVoucher(VoucherRecord voucher, Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> funcModifyTransactions = null)
			//{
			//	if (funcModifyTransactions == null)
			//		funcModifyTransactions = val => val;
			//	return voucher.ToString() + "\n\t" + string.Join("\n\t", funcModifyTransactions(voucher.Transactions).Select(t => t.ToString()));
			//}
		}
	}
}
