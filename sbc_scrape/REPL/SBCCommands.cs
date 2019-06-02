using MediusFlowAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
		public override async Task<object> Evaluate(List<object> parms) => await main.CreateIndex();
	}

	class CreateGroupedCmd : Command
	{
		private readonly Main main;
		public CreateGroupedCmd(Main main) => this.main = main;
		public override string Id => "creategrouped";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var summaries = await main.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2010, 1, 1));

			var accountDescriptions = summaries.Select(s => new { s.AccountId, s.AccountName }).Distinct()
				.ToDictionary(s => s.AccountId, s => s.AccountName);

			var aggregated = main.AggregateByTimePeriodAndFunc(summaries, 
				inGroup => inGroup.Sum(o => o.GrossAmount),
				accountSummary => accountSummary.AccountId ?? 0,
				TimeSpan.FromDays(7));

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

			var allGroupColumns = aggregated.Select(o => o.GroupedBy).Distinct().OrderBy(o => o).ToList();
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
			var defaultDates = new List<DateTime> { DateTime.Today.AddMonths(-3), DateTime.Today };
			var dates = parms.Select((p, i) => ParseArgument(parms, i, DateTime.MinValue)).ToList();
			for (int i = dates.Count; i < 2; i++)
				dates.Add(defaultDates[i]);

			var scraped = await main.Scrape(dates[0], dates[1], true, true);
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
