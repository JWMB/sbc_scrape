using NodaTime;
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
		public async Task Test1()
		{
			var roots = await ReadSIEFiles(new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
			var allVouchers = roots.SelectMany(o => o.Children).Where(o => o is VoucherRecord).Cast<VoucherRecord>();
			var matchResult = VoucherRecord.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);

			var annoyingAccountIds = new[] { 24400, 26410 }.ToList();
			Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> txFilter = txs =>
				TransactionRecord.PruneCorrections(txs).Where(t => !annoyingAccountIds.Contains(t.AccountId));

			var multiAccount = matchResult.Matched.Where(mv => txFilter(mv.slr.Transactions).Count() > 1);
			//var multiAccount = matchedVouchers.Where(mv => TransactionRecord.PruneCorrections(mv.slr.Transactions).Select(t => t.AccountId).Except(new[] { 24400, 26410 }).Count() > 1);
			var dbg2 = string.Join("\n\n", matchResult.Matched.OrderBy(o => o.other.Date)
				.Select(mv => $"{PrintVoucher(mv.slr, txFilter)}\n{PrintVoucher(mv.other, txFilter)}"));

			var cc = matchResult.Matched
				.Select(o => new {
					o.other.Date,
					txFilter(o.slr.Transactions).First().Amount,
					txFilter(o.slr.Transactions).First().AccountId,
					txFilter(o.slr.Transactions).First().CompanyName,
					Comment = "",
				})
				.Concat(matchResult.UnmatchedOther
					.Select(o => new {
						o.Date,
						txFilter(o.Transactions).First().Amount,
						AccountId = 0,
						txFilter(o.Transactions).First().CompanyName,
						Comment = o.VoucherTypeCode,
					}))
				.Concat(matchResult.UnmatchedSLR
					.Select(o => new {
						o.Date,
						txFilter(o.Transactions).First().Amount,
						AccountId = 0,
						txFilter(o.Transactions).First().CompanyName,
						Comment = o.VoucherTypeCode,
					}));
			cc = cc.OrderBy(o => o.Date);
			var dbg = string.Join("\n", cc.Select(o => $"{o.Date.AtMidnight().ToDateTimeUnspecified().ToString("yyyy-MM-dd")}\t{o.Comment}\t{o.Amount}\t{o.AccountId}\t{o.CompanyName}"));
			//bool filterAccountIds(int accountId) => ((accountId / 10000 != 1) || accountId == 19420 || accountId == 16300) && accountId != 27180 && accountId != 27300;


			string PrintVoucher(VoucherRecord voucher, Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> funcModifyTransactions = null)
			{
				if (funcModifyTransactions == null)
					funcModifyTransactions = val => val;
				return voucher.ToString() + "\n\t" + string.Join("\n\t", funcModifyTransactions(voucher.Transactions).Select(t => t.ToString()));
			}
		}

		[Fact]
		public void ParseLine()
		{
			var items = SIERecord.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

		private class AccountInfo
		{
			public string Name { get; set; }
			public string Source { get; set; }
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
			//var files = new[] { "output_2016.se", "output_2017.se", "output_2018.se" };
			var tasks = files.Select(async file => await SIERecord.Read(Path.Combine(sieDir, file)));
			await Task.WhenAll(tasks);
			//var root = await SIERecord.Read(path);
			return tasks.Select(o => o.Result).ToList();
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
