using REPL;
using sbc_scrape.SBC;
using SBCScan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
	class FetchSBCHtml : Command
	{
		private readonly Main main;
		public FetchSBCHtml(Main main) => this.main = main;
		public override string Id => "fetchsbc";
		public static HtmlSource GetHtmlSourceInstance(string typeName)
		{
			switch (typeName)
			{
				case "BankTransaction":
				case "x":
					return new BankTransactionSource();
				case "Receipt":
				case "r":
					return new ReceiptsSource();
				case "Invoice":
				case "i":
				default:
					return new InvoiceSource();
			}
		}

		static Regex rxDate = new Regex(@"^(?<year>\d{2,4})-?(?<month>\d{1,2})?");
		public static DateTime? ParseDate(string input)
		{
			var m = rxDate.Match(input);
			if (!m.Success)
				return null;
			var year = int.Parse(m.Groups["year"].Value);
			year = year < 100 ? year + (year < 30 ? 2000 : 1900) : year;
			var month = m.Groups["month"].Success ? int.Parse(m.Groups["month"].Value) : 1;
			return new DateTime(year, month, 1);
		}

		public override async Task<object> Evaluate(List<object> parms)
		{
			var start = new DateTime(DateTime.Today.Year, 1, 1);
			var end = new DateTime(start.Year, 12, 1);
			if (parms.Count > 1)
			{
				start = ParseDate(parms[1].ToString()) ?? start;
				end = new DateTime(start.Year, 12, 1);
				if (parms.Count > 2)
					end = ParseDate(parms[2].ToString()) ?? end;
			}

			var src = GetHtmlSourceInstance(parms[0] as string);

			return await Execute(main, src, start, end);
		}

		public static async Task<List<object>> Execute(Main main, HtmlSource src, DateTime start, DateTime end)
		{
			var result = new List<object>();
			for (var year = start.Year; year <= end.Year; year++)
			{
				var monthFrom = year == start.Year ? start.Month : 1;
				var monthTo = year == end.Year ? end.Month : 12;
				var html = main.SBC.FetchHtmlSource(src.UrlPath, year, monthFrom, monthTo).Result;
				var filenameSuffix = $"{year}{(monthFrom == 1 && monthTo == 12 ? "" : $"_{monthFrom}-{monthTo}")}";
				File.WriteAllText(Path.Combine(
					GlobalSettings.AppSettings.StorageFolderSbcHtml, string.Format(src.FilenamePattern, filenameSuffix)), html);
				result.AddRange(src.ParseObjects(html));
			}
			return result;
		}
	}
}
