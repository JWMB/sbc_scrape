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
			var path = @"C:\Users\jonas\Downloads\output_2016.se"; //output_20190929
			var root = await SIERecord.Read(path);
			var filterVoucherTypes = "AV BS FAS MA".Split(' ').ToList();
			var vouchers = root.Children.Where(o => o is VoucherRecord vr && !filterVoucherTypes.Contains(vr.VoucherTypeCode))
				.Cast<VoucherRecord>().ToList();

			VoucherRecord.NormalizeCompanyNames(vouchers);
			var groupedByCompany = vouchers.GroupBy(o => o.Transactions.First().CompanyName).ToDictionary(o => o.Key, o => o.ToList());
			//var sortedNames = grouped.Keys.OrderBy(o => o).ToList();
			var matchedVouchers = new List<(VoucherRecord slr, VoucherRecord other)>();
			var dbg = "";
			foreach (var kv in groupedByCompany)
			{
				var slrs = kv.Value.Where(o => o.VoucherType == VoucherType.SLR);
				var byAmount = kv.Value.GroupBy(o => FindRelevantAmount(o.Transactions)).ToDictionary(o => o.Key, o => o.ToList());

				foreach (var slr in slrs)
				{
					var amount = FindRelevantAmount(slr.Transactions);
					if (byAmount.TryGetValue(amount, out var list))
					{
						
						var hits = list.Where(o => o != slr && o.Date >= slr.Date && Period.Between(slr.Date, o.Date).Days < 35).ToList();
						if (hits.Count() == 1)
							matchedVouchers.Add((slr, hits.First()));
						else
						{ }
					}
				}

				if (byAmount.Count() > 1)
				{
					dbg += $"-- {kv.Key}\n";
					foreach (var grp in byAmount)
					{
						dbg += $"- {grp.Key}\n" + string.Join("\n", grp.Value.Select(o => PrintVoucher(o))) + "\n";
					}
					dbg += $"\n\n";
				}
				decimal FindRelevantAmount(IEnumerable<TransactionRecord> txs)
				{
					return txs.Select(o => Math.Abs(o.Amount)).Max();
				}
			}

			var annoyingAccountIds = new[] { 24400, 26410 }.ToList();
			Func<IEnumerable<TransactionRecord>, IEnumerable<TransactionRecord>> txFilter = txs =>
				TransactionRecord.PruneCorrections(txs).Where(t => !annoyingAccountIds.Contains(t.AccountId));

			var multiAccount = matchedVouchers.Where(mv => txFilter(mv.slr.Transactions).Count() > 1);
			//var multiAccount = matchedVouchers.Where(mv => TransactionRecord.PruneCorrections(mv.slr.Transactions).Select(t => t.AccountId).Except(new[] { 24400, 26410 }).Count() > 1);
			var dbg2 = string.Join("\n\n", matchedVouchers.Select(mv =>
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
