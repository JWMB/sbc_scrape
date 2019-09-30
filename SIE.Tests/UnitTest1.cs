using System;
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
			var path = @"C:\Users\jonas\Downloads\output_20190929.se";
			var root = await SIERecord.Read(path);
			var filterVoucherTypes = "AV BS FAS".Split(' ').ToList();
			var vouchers = root.Children.Where(o => o is VoucherRecord vr && !filterVoucherTypes.Contains(vr.VoucherTypeCode))
				.Cast<VoucherRecord>().ToList();
			bool filterAccountIds(int accountId) => (accountId / 10000 != 1) || accountId == 19420 || accountId == 16300;
			var str1 = string.Join("\n", vouchers.OrderBy(o => o.Date).Select(o => o.ToHierarchicalString()));
			var str = root.ToHierarchicalString();
		}

		[Fact]
		public void Test2()
		{
			var items = SIERecord.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

	}
}
