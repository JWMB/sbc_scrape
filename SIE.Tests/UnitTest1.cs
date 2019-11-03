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
			var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			var files = new[] { "output_2016.se", "output_2017.se" };
			var tasks = files.Select(async file => await SIERecord.Read(Path.Combine(sieDir, file)));
			await Task.WhenAll(tasks);
			//var root = await SIERecord.Read(path);
			var roots = tasks.Select(o => o.Result).ToList();
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

		[Fact]
		public void ParseAddress()
		{
			var items = SIERecord.ParseLine(@"#ADRESS ""SvenSvensson"" ""Box 21"" ""21120   MALMÖ"" ""040 - 12345""");
			var record = new AddressRecord();
			record.Read(items);
			
			Assert.Equal("040 - 12345", record.PhoneNumber);
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
