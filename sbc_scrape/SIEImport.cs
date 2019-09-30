using SIE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sbc_scrape
{
	class SIEImport
	{
		public async Task X()
		{
			var path = @"C:\Users\jonas\Downloads\output_20190929.se";
			var root = await SIERecord.Read(path);
			var vouchers = root.Children.Where(o => o is VoucherRecord vr && vr.VoucherTypeCode.StartsWith("LB")).Cast<VoucherRecord>().ToList();
			//vouchers.SelectMany(o => o.Transactions.Where(t => t.AccountId)
			var str = root.ToHierarchicalString();
		}
	}
}
