﻿using MediusFlowAPI;
using Newtonsoft.Json;
using OCR;
using REPL;
using sbc_scrape.SBC;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
	class InitCmd : Command, IUpdateCommandList
	{
		private readonly Main main;
		public InitCmd(Main main) => this.main = main;
		public override string Id => "init";
		public async Task<string> Evaluate()
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
						new FetchSIE(main),
						//new FetchSBCInvoices(main),
						//new UpdateInvoices(main),
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
		public async Task<string> Evaluate()
		{
			var invoices = await CreateIndexCmd.LoadInvoicesConsole(Console, main); // (await main.LoadInvoices(includeOCRd: false, (i, l) => Console.RewriteLine($"{i}/{l}"))).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
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
		public async Task<List<InvoiceSummary>> Evaluate()
		{
			return await LoadInvoicesConsole(Console, main);
			//Console.WriteLine("0");
			//return (await main.LoadInvoices(includeOCRd: false, (i, l) => Console.RewriteLine($"{i}/{l}"))).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
			//return (await main.LoadInvoices(true)).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
		}

		public static async Task<List<InvoiceSummary>> LoadInvoicesConsole(ConsoleBase console, Main main)
		{
			console.WriteLine("0");
			return (await main.LoadInvoices(includeOCRd: false, (i, l) => {
				if (l < 300 || (i % 10 == 0))
					console.RewriteLine($"{i}/{l}");
			}))
				.OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1))
				.ToList();
		}
	}

	class ObjectToFilenameAndObject : Command
	{
		public ObjectToFilenameAndObject() { }
		public override string Id => "o2fo";
		public object Evaluate(IEnumerable<object> ienum)
		{
			//var ienum = parms[0] as System.Collections.IEnumerable;
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
		public object Evaluate()
		{
			//if (!parms.Any())
			//	throw new ArgumentException("No HTML Source type defined");
			//var src = FetchSBCHtml.GetHtmlSourceInstance(parms[0] as string);
			var src = new InvoiceSource();
			var result = src.ReadAllObjects(defaultFolder);
			if (src is InvoiceSource invSrc)
			{
				return result.Select(r => (r as Invoice).ToSummary()).ToList();
			}
			throw new NotImplementedException("");
		}
	}

	class UpdateAll : Command
	{
		private readonly Main main;
		public UpdateAll(Main main) => this.main = main;
		public override string Id => "update";
		public async Task Evaluate()
		{
			var htmlSources = new HtmlSource[] { new BankTransactionSource(), new ReceiptsSource(), new InvoiceSource() };

			{
				//MediusFlow
				//var mediusExisting = await main.MediusFlow.LoadInvoiceSummaries(ff => ff.InvoiceDate.Year == year);
				await main.MediusFlow.Scrape();
				//TODO: check diff with existing files and return what was updated/added
			}

			//TODO: start from latest year in downloaded data and continue to current year
			var year = DateTime.Today.Year;

			foreach (var src in htmlSources)
			{
				//TODO: check latest locally stored record for each src - may need to fetch last year as well
				//TODO: some requests to SBC time out, need to split year into two parts
				try
				{
					var html = await main.SBC.FetchHtmlSource(src.UrlPath, year);
					File.WriteAllText(Path.Combine(
						GlobalSettings.AppSettings.StorageFolderSbcHtml, string.Format(src.FilenamePattern, year)), html);
				}
				catch (NotSupportedException ex)
				{
					// Ignore - combo not supported by SBC (e.g. Receipts after 2020)
				}
			}

			{
				// TODO: disable for now b/c of encoding problem with SIE fetch
				//var sieCmd = new FetchSIE(main);
				//await sieCmd.Evaluate();
				Console.WriteLine($"SIE download disabled b/c encdding problem, please download from {SBC.SBCMain.MainUrlSIE}, and move to folder {GlobalSettings.AppSettings.StorageFolderSIE} (see naming convention)");
			}
		}
	}

	//class UpdateInvoices : Command
	//{
	//	private readonly Main main;
	//	public UpdateInvoices(Main main) => this.main = main;
	//	public override string Id => "updateinvoices";
	//	public override async Task<object> Evaluate(List<object> parms)
	//	{
	//		await UpdateAll.Execute(main, new HtmlSource[] { new InvoiceSource() });
	//		return null;
	//	}
	//}

	class FetchSIE : Command
	{
		private readonly Main main;
		public FetchSIE(Main main) => this.main = main;
		public override string Id => "fetchsie";

		private static string filenameFormat = "output_YEAR.se";
		public static List<int> GetExistingYears()
		{
			var existing = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderSIE).GetFiles(filenameFormat.Replace("YEAR", "*"));
			var rx = new Regex(Regex.Escape(filenameFormat).Replace("YEAR", @"(\d{4})"));
			return existing.Select(o => rx.Match(o.Name))
				.Where(o => o.Success)
				.Select(o => int.Parse(o.Groups[1].Value))
				.ToList();
		}

		public async Task<bool> Evaluate(IEnumerable<int> years = null)
		{
			if (years == null)
			{
				var existingYears = GetExistingYears();
				var numYearsBack = 10;
				years = Enumerable.Range(DateTime.Today.Year - numYearsBack, numYearsBack)
					.Except(existingYears);
				if (!years.Contains(DateTime.Today.Year)) // Always update latest year
					years = years.Concat(new[] { DateTime.Today.Year });
			}

			Console?.WriteLine($"Fetch SIE for years {string.Join(",", years)}");
			foreach (var year in years)
			{
				Console?.WriteLine($"{year}");
				var data = await main.SBC.FetchSIEFile(year);
				File.WriteAllText(Path.Combine(
						GlobalSettings.AppSettings.StorageFolderSIE, filenameFormat.Replace("YEAR", year.ToString())), data);
			}
			return true;
		}
	}


	class CreateHouseIndexCmd : Command
	{
		private readonly Main main;
		public CreateHouseIndexCmd(Main main) => this.main = main;
		public override string Id => "houseindex";
		public async Task<string> Evaluate()
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
		public async Task<object> Evaluate(DateTime? start = null, DateTime? end = null)
		{
			//var dates = parms.Select((p, i) => ParseArgument(parms, i, DateTime.MinValue)).Cast<DateTime?>().ToList();
			//for (int i = dates.Count; i < 2; i++)
			//	dates.Add(null);

			//var scraped = await main.MediusFlow.Scrape(dates[0], dates[1], saveToDisk: true, goBackwards: false);
			var scraped = await main.MediusFlow.Scrape(start, end, saveToDisk: true, goBackwards: false);
			return scraped.Select(iv => $"{iv.Id} {iv.TaskId} {iv.InvoiceDate} {iv.Supplier} {iv.GrossAmount} {iv.AccountId} {iv.AccountName}");
		}
	}

	class GetImagesCmd : Command
	{
		private readonly Main main;

		public GetImagesCmd(Main main) => this.main = main;
		public override string Id => "getimages";
		public async Task<object> Evaluate(List<object> parms)
		{
			var invoiceIds = ParseArguments<long?>(parms, null);
			Func<InvoiceFull.FilenameFormat, bool> invoiceFilter;
			if (invoiceIds.Any() && !invoiceIds.Any(o => o == null))
			{
				var idsList = invoiceIds.Cast<long>().ToList();
				invoiceFilter = o => idsList.Contains(o.Id);
			}
			else
			{
				var defaultDates = new List<DateTime> { DateTime.Today.AddMonths(-1), DateTime.Today };
				var dates = ParseArguments(parms, DateTime.MinValue);
				for (int i = dates.Count; i < 2; i++)
					dates.Add(defaultDates[i]);

				Console.WriteLine($"Download images between {dates[0].ToShortDateString()} - {dates[1].ToShortDateString()}");
				invoiceFilter = o => o.InvoiceDate >= dates[0] && o.InvoiceDate <= dates[1];
			}

			var result = await main.MediusFlow.DownloadPdfs(invoiceFilter); //DownloadImages
			return string.Join("\n", result.Select(kv => $"{InvoiceFull.FilenameFormat.Create(kv.Key)}: {string.Join(',', kv.Value)}"));
		}
	}

	class OCRImagesCmd : Command
	{
		public override string Id => "ocr";

		public string Evaluate()
		{
			var folder = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderDownloadedFiles);
			var files = folder.GetFiles("*.png");
			var alreadyProcessed = folder.GetFiles("*.txt").Select(o => Path.GetFileNameWithoutExtension(o.Name)).ToList();
			var filesToProcess = files.Where(o => !alreadyProcessed.Contains(Path.GetFileNameWithoutExtension(o.Name)));

			var processed = new List<string>();
			var index = 0;
			Console.WriteLine($"Found {filesToProcess.Count()} unprocessed files");
			foreach (var file in filesToProcess)
			{
				var ocrFile = file.FullName.Remove(file.FullName.Length - file.Extension.Length) + ".txt";
				var started = DateTime.Now;
				var text = new TesseractOCR(GlobalSettings.AppSettings.PathToTesseract).Run(file.FullName, new string[] { "swe", "eng" });
				var elapsed = DateTime.Now - started;
				File.WriteAllText(ocrFile, text);
				Console.WriteLine($"{index++}/{filesToProcess.Count()} {file.Name}");
				processed.Add(ocrFile);
			}
			return string.Join("\n", processed);
		}
	}
}
