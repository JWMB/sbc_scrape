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
						new UpdateAll(main),
					});
			return currentCommandList.Concat(new Command[] { new ListCmd(currentCommandList) });
		}
	}

	class GetAccountsListCmd : Command
	{
		private readonly Main main;
		public GetAccountsListCmd(Main main) => this.main = main;
		public override string Id => "accounts";
		public override async Task<object> Evaluate(List<object> parms)
		{
			Console.WriteLine("0");
			var invoices = (await main.LoadInvoices(includeOCRd: false, (i, l) => Console.RewriteLine($"{i}/{l}"))).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
			var tmp = invoices.GroupBy(o => o.AccountId).ToDictionary(o => o.Key, o => new { Total = o.Sum(g => g.GrossAmount), Names = o.Select(g => g.AccountName).Distinct() });
			var result = string.Join("\n",
				tmp.Select(kv => $"{kv.Key}\t{string.Join(',', kv.Value.Names)}\t{kv.Value.Total}")
				);
			//var tmp = invoices.Select(o => new { o.AccountName, o.AccountId }).Distinct(); //.ToDictionary(o => o.AccountName, o => o.AccountId);
			return result;
		}
	}

	class CreateIndexCmd : Command
	{
		private readonly Main main;
		public CreateIndexCmd(Main main) => this.main = main;
		public override string Id => "createindex";
		public override async Task<object> Evaluate(List<object> parms)
		{
			Console.WriteLine("0");
			return (await main.LoadInvoices(includeOCRd: false, (i, l) => Console.RewriteLine($"{i}/{l}"))).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
			//return (await main.LoadInvoices(true)).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
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

	class UpdateAll : Command
	{
		private readonly Main main;
		public UpdateAll(Main main) => this.main = main;
		public override string Id => "update";
		public override async Task<object> Evaluate(List<object> parms)
		{
			await Execute(main, new HtmlSource[] { new BankTransactionSource(), new ReceiptsSource(), new InvoiceSource() });
			return null;
		}

		public static async Task Execute(Main main, IEnumerable<HtmlSource> htmlSources)
		{
			{
				//MediusFlow
				//var mediusExisting = await main.MediusFlow.LoadInvoiceSummaries(ff => ff.InvoiceDate.Year == year);
				var scraped = await main.MediusFlow.Scrape();
				//TODO: check diff with existing files and return what was updated/added
			}

			//TODO: start from latest year in downloaded data and continue to current year
			var year = DateTime.Today.Year;

			foreach (var src in htmlSources)
			{
				//TODO: check latest locally stored record for each src - may need to fetch last year as well
				//TODO: some requests to SBC time out, need to split year into two parts
				var html = await main.SBC.FetchHtmlSource(src.UrlPath, year);
				File.WriteAllText(Path.Combine(
					GlobalSettings.AppSettings.StorageFolderSbcHtml, string.Format(src.FilenamePattern, year)), html);
			}
		}

	}

	class UpdateInvoices : Command
	{
		private readonly Main main;
		public UpdateInvoices(Main main) => this.main = main;
		public override string Id => "updateinvoices";
		public override async Task<object> Evaluate(List<object> parms)
		{
			await UpdateAll.Execute(main, new HtmlSource[] { new InvoiceSource() });
			return null;
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

	class ScrapeCmd : Command
	{
		private readonly Main main;
		public ScrapeCmd(Main main) => this.main = main;
		public override string Id => "scrape";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var dates = parms.Select((p, i) => ParseArgument(parms, i, DateTime.MinValue)).Cast<DateTime?>().ToList();
			for (int i = dates.Count; i < 2; i++)
				dates.Add(null);

			var scraped = await main.MediusFlow.Scrape(dates[0], dates[1], saveToDisk: true, goBackwards: false);
			return scraped.Select(iv => $"{iv.Id} {iv.TaskId} {iv.InvoiceDate} {iv.Supplier} {iv.GrossAmount} {iv.AccountId} {iv.AccountName}");
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
