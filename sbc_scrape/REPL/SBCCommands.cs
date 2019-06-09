﻿using MediusFlowAPI;
using Newtonsoft.Json;
using REPL;
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
						new GetInvoiceCmd(main.CreateScraper()),
						new GetTaskCmd(main.CreateApi()),
						new GetImagesCmd(main),
						new ScrapeCmd(main),
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
			return (await main.LoadInvoiceSummaries()).OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
		}
	}

	class Summaries : Command
	{
		private readonly Main main;
		public Summaries(Main main) => this.main = main;
		public override string Id => "summaries";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var summaries = await main.LoadInvoiceSummaries();
			var pathToOCRed = GlobalSettings.AppSettings.StorageFolderDownloadedFilesResolved;
			var ocrFiles = new DirectoryInfo(pathToOCRed).GetFiles("*.txt");
			foreach (var summary in summaries)
			{
				var found = summary.InvoiceImageIds?.Select(v => new { Guid = v, File = ocrFiles.SingleOrDefault(f => f.Name.Contains(v)) })
					.Where(f => f.File != null).Select(f => new { Guid = f.Guid, Content = File.ReadAllText(f.File.FullName) });
				summary.InvoiceTexts = string.Join("\n", found.Select(f =>
					$"{f.Guid}: {string.Join('\n', f.Content.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))}")
					);
			}
			return summaries.OrderByDescending(r => r.InvoiceDate ?? new DateTime(1900, 1, 1)).ToList();
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

	class ReadSBCInvoices : Command
	{
		private readonly string defaultFolder;
		public ReadSBCInvoices(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "sbcinvoices";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var read = sbc_scrape.Fakturaparm.ReadAll(defaultFolder).Select(o => o.ToSummary()).ToList();
			var parm1 = parms.FirstOrDefault();
			if (parm1 is IEnumerable<InvoiceSummary> input)
			{
				//Use "real" data instead of Fakturaparm where they overlap:
				var duplicatesRemoved = read.Except(input, new sbc_scrape.Fakturaparm.SBCvsMediusEqualityComparer())
					.Concat(input)
					.OrderByDescending(o => o.InvoiceDate);
				return duplicatesRemoved;
			}
			return read;
		}
	}


	class CreateHouseIndexCmd : Command
	{
		private readonly Main main;
		public CreateHouseIndexCmd(Main main) => this.main = main;
		public override string Id => "houseindex";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var summaries = await main.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2001, 1, 1));
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
				summaries = await main.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2010, 1, 1));

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

			var aggregated = main.AggregateByTimePeriodAndFunc(summaries, 
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

	class GetTaskCmd : Command
	{
		private readonly API api;
		public GetTaskCmd(API api) => this.api = api;
		public override string Id => "gettask";
		public override async Task<object> Evaluate(List<object> parms)
		{
			if (Command.TryParseArgument<long>(parms, 0, out var id))
				return JsonConvert.SerializeObject(await api.GetTask(id));
			return $"id '{parms.FirstOrDefault()}' not parseable";
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

			var scraped = await main.Scrape(dates[0], dates[1], saveToDisk: false, goBackwards: false);
			var shortSummary = scraped.Select(iv => new {
				iv.Id,
				iv.TaskId,
				iv.InvoiceDate,
				iv.Supplier,
				iv.GrossAmount,
				iv.AccountId,
				iv.AccountName,
			});
			return ServiceStack.Text.CsvSerializer.SerializeToString(shortSummary);
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

			var result = await main.DownloadImages(dates[0], dates[1]);
			return string.Join("\n", result.Select(kv => $"{InvoiceFull.FilenameFormat.Create(kv.Key)}: {string.Join(',', kv.Value)}"));
		}
	}

	class OCRImagesCmd : Command
	{
		public override string Id => "ocr";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var files = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderDownloadedFilesResolved).GetFiles("*.png");
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

	class ConvertInvoiceImageFilenameCmd : Command
	{
		private readonly Main main;
		public ConvertInvoiceImageFilenameCmd(Main main) => this.main = main;
		public override string Id => "convii";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var summaries = await main.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2001, 1, 1));
			var dir = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderDownloadedFilesResolved);
			foreach (var ext in new string[] { ".png", ".txt" })
			{
				var files = dir.GetFiles("*" + ext);
				var fileNames = files.Select(f => f.Name.Remove(f.Name.Length - ext.Length)).ToList();
				foreach (var summary in summaries)
				{
					var filenameFormat = InvoiceFull.GetFilenamePrefix(summary.InvoiceDate.Value, summary.Supplier, summary.Id) + "_{0}";
					var found = summary.InvoiceImageIds.Where(id => fileNames.Contains(id));
					foreach (var f in found)
					{
						var newName = string.Format(filenameFormat, f);
						File.Move(Path.Combine(dir.FullName, f + ext), Path.Combine(dir.FullName, newName + ext));
					}
				}
			}
			return "";
		}
	}

	class GetInvoiceCmd : Command
	{
		private readonly InvoiceScraper scraper;

		public GetInvoiceCmd(InvoiceScraper scraper) => this.scraper = scraper;
		public override string Id => "getinvoice";
		public override async Task<object> Evaluate(List<object> parms)
		{
			if (Command.TryParseArgument<long>(parms, 0, out var id))
				return JsonConvert.SerializeObject(await scraper.GetInvoice(id));
			return $"id '{parms.FirstOrDefault()}' not parseable";
		}
	}
}
