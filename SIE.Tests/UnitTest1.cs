using NodaTime;
using SIE.Matching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SIE.Tests
{
	public class UnitTest1
	{
		[Fact]
		public async Task TestX()
		{
			var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
			var roots = await ReadSIEFiles(files);
			var allVouchers = roots.SelectMany(o => o.Children).Where(o => o is VoucherRecord).Cast<VoucherRecord>();

			var ma = string.Join("\n", allVouchers.Where(o => o.VoucherTypeCode == "MA").OrderBy(o => o.Date).Select(o => o.ToHierarchicalString()));

			var withSalaries = allVouchers.Where(o => !(new[] { "LR", "BS", "MA", "AR" }.Contains(o.VoucherTypeCode))
				&& o.Transactions.Any(t => t.AccountId.ToString().StartsWith("647"))).OrderBy(o => o.Date); //27300
			var dbg = string.Join("\n", withSalaries.Select(o => o.ToHierarchicalString()));
			//var salaries = allVouchers.Where(o => o.VoucherType == VoucherType.Salary).OrderBy(o => o.Date).ToList();
		}


		[Fact]
		public async Task TestCreateMatched()
		{
			var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
			var roots = await ReadSIEFiles(files); // new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
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
			var roots = await ReadSIEFiles(files); // new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
			var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

			var annoyingAccountIds = new[] { 24400, 26410 }.ToList();
			Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> txFilter = txs =>
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

		[Fact]
		public void ParseLine()
		{
			var items = SIERecord.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

		private class AccountInfo
		{
			public string Name { get; set; } = string.Empty;
			public string Source { get; set; } = string.Empty;
		}

		[Fact]
		public async Task GetAccounts()
		{
			var roots = await ReadSIEFiles(new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
			var result = roots.SelectMany(o => o.Children.OfType<AccountRecord>()).GroupBy(o => o.AccountId).Select(o => o.First()).ToDictionary(o => o.AccountId, o => new AccountInfo { Name = o.AccountName, Source = "SIE" });
			//string.Join("\n",  Select(o => $"{o.AccountId}\t{o.AccountName}"));

			var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			var tmp = File.ReadAllText(Path.Combine(sieDir, "accountsexport.txt"));
			var xx = tmp.Split('\n').Skip(1).Where(o => o.Length > 0).Select(line => line.Split('\t')).ToDictionary(o => int.Parse(o[0]), o => o[1].Trim());

			foreach (var kv in xx)
			{
				if (!result.ContainsKey(kv.Key))
					result.Add(kv.Key, new AccountInfo { Name = $"{kv.Value}", Source = "" });
				else
				{
					result[kv.Key].Source = "";
					if (result[kv.Key].Name != kv.Value)
						result[kv.Key].Name += $" - {kv.Value}";
				}
			}

			var str = string.Join("\n", result.Keys.OrderBy(o => o).Select(o => $"{o}\t{result[o].Name}\t{result[o].Source}"));
		}

		[Fact]
		public void ParseAddress()
		{
			var items = SIERecord.ParseLine(@"#ADRESS ""SvenSvensson"" ""Box 21"" ""21120   MALMÖ"" ""040 - 12345""");
			var record = new AddressRecord();
			record.Read(items);
			
			Assert.Equal("040 - 12345", record.PhoneNumber);
		}

		async Task<List<RootRecord>> ReadSIEFiles(IEnumerable<string> files)
		{
			var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			return await SBCExtensions.ReadSIEFiles(files.Select(file => Path.Combine(sieDir, file)));

			//var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			//var tasks = files.Select(async file => await SIERecord.Read(Path.Combine(sieDir, file)));
			//await Task.WhenAll(tasks);
			//var result = tasks.Select(o => o.Result).ToList();

			//result.SelectMany(o => o.Children).OfType<VoucherRecord>().SelectMany(o => o.Transactions).ToList()
			//	.ForEach(o => o.PreProcessCompanyName());

			//return result;
		}

		string GetCurrentOrSolutionDirectory()
		{
			var sep = "\\" + Path.DirectorySeparatorChar;
			var rx = new System.Text.RegularExpressions.Regex($@".*(?={sep}[^{sep}]+{sep}bin)");
			var m = rx.Match(Directory.GetCurrentDirectory());
			return m.Success ? m.Value : Directory.GetCurrentDirectory();
		}
	}
}
