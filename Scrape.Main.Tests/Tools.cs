using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scrape.Main.Tests
{
	public class Tools
	{
		public static string GetOutputFolder(params string[] subpaths)
		{
			var dir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped");
			var arr = new[] { dir }.Concat(subpaths);
			return arr.Aggregate((p, c) => Path.Join(p, c));
		}

		public static string GetCurrentOrSolutionDirectory()
		{
			var sep = "\\" + Path.DirectorySeparatorChar;
			var rx = new System.Text.RegularExpressions.Regex($@".*(?={sep}[^{sep}]+{sep}bin)");
			var m = rx.Match(Directory.GetCurrentDirectory());
			return m.Success ? m.Value : Directory.GetCurrentDirectory();
		}

		public static async Task<List<sbc_scrape.SBC.Invoice>> LoadSBCInvoices(Func<int, bool> accountFilter)
		{
			var dir = GetOutputFolder("sbc_html");
			var tmp = new List<sbc_scrape.SBC.Invoice>();
			await foreach (var sbcRows in new sbc_scrape.SBC.InvoiceSource().ReadAllAsync(dir))
				tmp.AddRange(sbcRows.Where(o => accountFilter(o.AccountId)));
			return tmp;
		}

	}
}
