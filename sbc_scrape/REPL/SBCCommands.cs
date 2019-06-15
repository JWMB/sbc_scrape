using MediusFlowAPI;
using Newtonsoft.Json;
using REPL;
using sbc_scrape.SBC;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
	class InitCmd : Command, IUpdateCommandList
	{
		private readonly Main main;
		public InitCmd(Main main) => this.main = main;
		public override string Id => "init";
		public override async Task<object> Evaluate(List<object> parms)
		{
			await main.Init();
			return "Initialized session";
		}

		public IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList)
		{
			currentCommandList = currentCommandList.Where(c => !(c is ListCmd))
				.Concat(new Command[] {
						new GetInvoiceCmd(main.MediusFlow.CreateScraper()),
						new GetTaskCmd(main.MediusFlow.CreateApi()),
						new GetImagesCmd(main),
						new ScrapeCmd(main),
						new FetchSBCHtml(main),
						//new FetchSBCInvoices(main),
						new UpdateInvoices(main),
					});
			return currentCommandList.Concat(new Command[] { new ListCmd(currentCommandList) });
		}
	}

	class CreateIndexCmd : Command
	{
		private readonly Main main;
		public CreateIndexCmd(Main main) => this.main = main;
		public override string Id => "createindex";
		public override async Task<object> Evaluate(List<object> parms)
		{
			return (await main.LoadInvoices(false)).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
		}
	}

	class ObjectToFilenameAndObject : Command
	{
		public ObjectToFilenameAndObject() { }
		public override string Id => "o2fo";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var t = parms[0].GetType();
			var ienum = parms[0] as System.Collections.IEnumerable;
			var result = new Dictionary<string, object>();
			foreach (var item in ienum)
			{
				if (item is InvoiceSummary isum)
				{
					var filename = $"{InvoiceFull.FilenameFormat.Create(isum)}.json";
					result.Add(filename, JsonConvert.SerializeObject(isum, Formatting.Indented));
				}
			}
			return result;
		}
	}

	class InvoiceTransactionJoin : Command
	{
		private readonly string defaultFolder;
		private readonly Main main;

		public InvoiceTransactionJoin(string defaultFolder, Main main)
		{
			this.defaultFolder = defaultFolder;
			this.main = main;
		}
		public override string Id => "jit";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var invoices = (await main.LoadInvoices(false)).Where(o => o.DueDate.HasValue).ToList();
			var receipts = new ReceiptsSource().ReadAll(defaultFolder);
			//var transactions = new BankTransactionSource().ReadAll(defaultFolder);

			return new sbc_scrape.DataJoiner().Evaluate(invoices, receipts);
		}
	}

	class ReadSBCHtml : Command
	{
		private readonly string defaultFolder;
		public ReadSBCHtml(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "sbc";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var src = FetchSBCHtml.GetHtmlSourceInstance(parms[0] as string);
			var result = src.ReadAllObjects(defaultFolder);
			if (src is InvoiceSource invSrc)
			{
				result = result.Select(r => (r as Invoice).ToSummary()).Cast<object>().ToList();
			}
			return result;
		}
	}

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

		static System.Text.RegularExpressions.Regex rxDate = new System.Text.RegularExpressions.Regex(@"^(?<year>\d{2,4})-?(?<month>\d{1,2})?");
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
			var src = GetHtmlSourceInstance(parms[0] as string);

			var start = new DateTime(DateTime.Today.Year, 1, 1);
			var end = new DateTime(start.Year, 12, 1);
			if (parms.Count > 1)
			{
				start = ParseDate(parms[1].ToString()) ?? start;
				end = new DateTime(start.Year, 12, 1);
				if (parms.Count > 2)
					end = ParseDate(parms[2].ToString()) ?? end;
			}

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

	class UpdateInvoices : Command
	{
		private readonly Main main;
		public UpdateInvoices(Main main) => this.main = main;
		public override string Id => "updateinvoices";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var year = DateTime.Today.Year;

			var src = new InvoiceSource();
			var html = await main.SBC.FetchHtmlSource(src.UrlPath, year);
			File.WriteAllText(Path.Combine(
				GlobalSettings.AppSettings.StorageFolderSbcHtml, string.Format(src.FilenamePattern, year)), html);

			var scraped = await main.MediusFlow.Scrape();

			//TODO: check diff with existing files and return what was updated/added

			return "";
		}
	}

	class CreateHouseIndexCmd : Command
	{
		private readonly Main main;
		public CreateHouseIndexCmd(Main main) => this.main = main;
		public override string Id => "houseindex";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var summaries = await main.MediusFlow.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2001, 1, 1));
			var withHouses = summaries.Select(s =>
				new { Summary = s, Houses = s.Houses?.Split(',').Select(h => h.Trim()).Where(h => h.Length > 0).ToList() })
				.Where(s => s.Houses != null && s.Houses.Any());

			var byHouse = withHouses.SelectMany(o => o.Houses.Select(h => new {
				House = h,
				OtherHouses = string.Join(",", o.Houses.Except(new string[] { h })),
				o.Summary }))
				.GroupBy(o => o.House).ToDictionary(o => o.Key, o => o.ToList());

			return string.Join('\n', byHouse.Select(o =>
				$"{o.Key}: {o.Value.Sum(v => v.Summary.GrossAmount)}\n  "
				+ $"{(string.Join("\n  ", o.Value.Select(v => ($"{v.Summary.GrossAmount} {v.Summary.AccountName} {v.Summary.InvoiceDate} {v.Summary.Supplier} {v.OtherHouses}"))))}"));

			//withHouses = withHouses.OrderBy(o => o.Houses.First()).ThenByDescending(o => o.Summary.InvoiceDate).ToList();
			//return string.Join('\n', withHouses.Select(o => 
			//$"{string.Join(',', o.Houses)}: {o.Summary.GrossAmount} {o.Summary.AccountName} {o.Summary.InvoiceDate} {o.Summary.Supplier}"));
		}
	}

	class CreateGroupedCmd : Command
	{
		private readonly Main main;
		public CreateGroupedCmd(Main main) => this.main = main;
		public override string Id => "creategrouped";
		public override async Task<object> Evaluate(List<object> parms)
		{
			List<InvoiceSummary> summaries = null;
			if (parms.FirstOrDefault() is IEnumerable<InvoiceSummary> input)
				summaries = input.ToList();
			else
				summaries = await main.LoadInvoices(false); // MediusFlow.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2010, 1, 1));

			var accountDescriptionsWithDups = summaries.Select(s => new { s.AccountId, s.AccountName }).Distinct();
			//We may have competing AccountNames (depending on source)
			var distinct = accountDescriptionsWithDups.Select(s => s.AccountId).Distinct();
			var accountDescriptions = distinct.Select(s => accountDescriptionsWithDups.First(d => d.AccountId == s))
				.ToDictionary(s => s.AccountId, s => s.AccountName);

			Func<InvoiceSummary, DateTime> timeBinSelector = null;
			if (false)
			{
				var bin = TimeSpan.FromDays(7);
				var tsAsTicks = bin.Ticks;
				timeBinSelector = invoice => new DateTime((invoice.InvoiceDate.Value.Ticks / tsAsTicks) * tsAsTicks);
			}
			else
			{
				timeBinSelector = invoice => new DateTime(invoice.InvoiceDate.Value.Year, invoice.InvoiceDate.Value.Month, 1);
			}

			var aggregated = main.MediusFlow.AggregateByTimePeriodAndFunc(summaries, 
				inGroup => inGroup.Sum(o => o.GrossAmount),
				accountSummary => accountSummary.AccountId ?? 0, //(accountSummary.AccountId ?? 0) / 1000 * 1000,
				timeBinSelector);

			// Hmm, the following pivot transform is daft, make it more generic and smarter:
			var byDateAndColumn = new Dictionary<DateTime, Dictionary<long, List<string>>>();
			foreach (var item in aggregated)
			{
				if (!byDateAndColumn.TryGetValue(item.TimeBin, out var byDate))
				{
					byDate = new Dictionary<long, List<string>>();
					byDateAndColumn.Add(item.TimeBin, byDate);
				}
				byDate.Add(item.GroupedBy, new List<string> { item.Aggregate.ToString(), string.Join(",", item.InvoiceIds) });
			}

			//Sort columns by total - most important columns to the left
			//Only look at last year
			var lookFromDate = aggregated.Max(o => o.TimeBin).AddYears(-1);
			var sorted = aggregated.GroupBy(o => o.GroupedBy).Select(g => new {
				GroupedBy = g.Key,
				Sum = g.Where(o => o.TimeBin > lookFromDate).Sum(o => o.Aggregate) }
			).OrderByDescending(o => o.Sum);
			var allGroupColumns = sorted.Select(o => o.GroupedBy).ToList();

			var table = new List<List<string>>();
			var header = new List<string> { "Date" }.
				Concat(allGroupColumns.Select(o => $"{o} {accountDescriptions[o]}")).ToList();
			table.Add(header);
			var emptyRow = header.Select(o => (string)null).ToList();
			foreach (var byDate in byDateAndColumn.OrderByDescending(k => k.Key))
			{
				var row = new List<string>(emptyRow);
				row[0] = byDate.Key.ToString("yyyy-MM-dd");

				table.Add(row);
				foreach (var kv in byDate.Value)
					row[allGroupColumns.IndexOf(kv.Key) + 1] = kv.Value[0];
				//TODO: for e.g. google spreadsheet, we want Value[1] as a comment or tag, so we can easily find underlying invoices
			}

			var tsv = string.Join('\n', table.Select(row => string.Join('\t', row)));
			return tsv;
		}
	}

	class ScrapeCmd : Command
	{
		private readonly Main main;
		public ScrapeCmd(Main main) => this.main = main;
		public override string Id => "scrape";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var defaultDates = new List<DateTime> { DateTime.Today.AddMonths(-1), DateTime.Today };
			var dates = parms.Select((p, i) => ParseArgument(parms, i, DateTime.MinValue)).ToList();
			for (int i = dates.Count; i < 2; i++)
				dates.Add(defaultDates[i]);

			var scraped = await main.MediusFlow.Scrape(dates[0], dates[1], saveToDisk: false, goBackwards: false);
			return scraped.Select(iv =>
			$"{iv.Id} {iv.TaskId} {iv.InvoiceDate} {iv.Supplier} {iv.GrossAmount} {iv.AccountId} {iv.AccountName}");
		}
	}

	class GetImagesCmd : Command
	{
		private readonly Main main;

		public GetImagesCmd(Main main) => this.main = main;
		public override string Id => "getimages";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var defaultDates = new List<DateTime> { DateTime.Today.AddMonths(-1), DateTime.Today };
			var dates = parms.Select((p, i) => ParseArgument(parms, i, DateTime.MinValue)).ToList();
			for (int i = dates.Count; i < 2; i++)
				dates.Add(defaultDates[i]);

			var result = await main.MediusFlow.DownloadImages(dates[0], dates[1]);
			return string.Join("\n", result.Select(kv => $"{InvoiceFull.FilenameFormat.Create(kv.Key)}: {string.Join(',', kv.Value)}"));
		}
	}

	class OCRImagesCmd : Command
	{
		public override string Id => "ocr";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var files = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderDownloadedFiles).GetFiles("*.png");
			var processed = new List<string>();
			foreach (var file in files)
			{
				var ocrFile = file.FullName.Remove(file.FullName.Length - file.Extension.Length) + ".txt";
				if (!File.Exists(ocrFile))
				{
					var started = DateTime.Now;
					var text = sbc_scrape.OCR.Run(file.FullName, new string[] { "swe", "eng" });
					var elapsed = DateTime.Now - started;
					File.WriteAllText(ocrFile, text);
					Console.WriteLine(ocrFile);
					processed.Add(ocrFile);
				}
			}
			return string.Join("\n", processed);
		}
	}
}
