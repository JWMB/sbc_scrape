using SBCScan.REPL;
using SIE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sbc_scrape.SBC
{
	class JoinTransactionSIE
	{

		public async Task TempTest()
		{
			var basePath = @"C:\Users\jonas\source\repos\sbc_scrape\sbc_scrape\scraped\";
			var path = basePath + @"SIE\output_2016.se"; //output_20190929
			var root = await SIERecord.Read(path);
			var matchResult = VoucherRecord.MatchSLRVouchers(root.Children.Where(o => o is VoucherRecord).Cast<VoucherRecord>(), VoucherRecord.DefaultIgnoreVoucherTypes);

			var src = new BankTransactionSource();
			var transactions = src.ReadAllObjects(basePath + @"scb_html\");
			//if (src is InvoiceSource invSrc)
			//{
			//	result = result.Select(r => (r as Invoice).ToSummary()).Cast<object>().ToList();
			//}

			//matchResult.
		}
	}
}
