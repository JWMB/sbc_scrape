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
			var path = @"C:\Users\jonas\source\repos\sbc_scrape\sbc_scrape\scraped\SIE\output_2016.se"; //output_20190929
			var root = await SIERecord.Read(path);
			var filterVoucherTypes = "AV BS FAS MA".Split(' ').ToList();
			var vouchers = root.Children.Where(o => o is VoucherRecord vr && !filterVoucherTypes.Contains(vr.VoucherTypeCode))
				.Cast<VoucherRecord>().ToList();

			var matchResult = VoucherRecord.MatchSLRVouchers(vouchers);

			var annoyingAccountIds = new[] { 24400, 26410 }.ToList();
			Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> txFilter = txs =>
				TransactionRecord.PruneCorrections(txs).Where(t => !annoyingAccountIds.Contains(t.AccountId));

			var multiAccount = matchResult.Matched.Where(mv => txFilter(mv.slr.Transactions).Count() > 1);
			//var multiAccount = matchedVouchers.Where(mv => TransactionRecord.PruneCorrections(mv.slr.Transactions).Select(t => t.AccountId).Except(new[] { 24400, 26410 }).Count() > 1);
			var dbg2 = string.Join("\n\n", matchResult.Matched.Select(mv =>
				$"{PrintVoucher(mv.slr, txFilter)}\n{PrintVoucher(mv.other, txFilter)}"));

			//bool filterAccountIds(int accountId) => ((accountId / 10000 != 1) || accountId == 19420 || accountId == 16300) && accountId != 27180 && accountId != 27300;
			var str = root.ToHierarchicalString();

			string PrintVoucher(VoucherRecord voucher, Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> funcModifyTransactions = null)
			{
				if (funcModifyTransactions == null)
					funcModifyTransactions = val => val;
				return voucher.ToString() + "\n\t" + string.Join("\n\t", funcModifyTransactions(voucher.Transactions).Select(t => t.ToString()));
			}
		}



		[Fact]
		public void Test2()
		{
			var items = SIERecord.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

	}
}
