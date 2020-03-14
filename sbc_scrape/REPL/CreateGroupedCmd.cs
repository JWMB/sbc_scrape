using MediusFlowAPI;
using REPL;
using SBCScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
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
			{
				//summaries = await main.LoadInvoices(false);
				Console.WriteLine("0");
				summaries = await main.MediusFlow.LoadAndTransformInvoicesCallback(o => InvoiceSummary.Summarize(o),
					(index, total) => { if (index % 50 == 0 || index > total - 10) { Console.RewriteLine($"{index}/{total}"); } });
			}

			var accountDescriptionsWithDups = summaries.Select(s => new { s.AccountId, s.AccountName }).Distinct();
			//We may have competing AccountNames (depending on source)
			var distinct = accountDescriptionsWithDups.Select(s => s.AccountId).Distinct();
			var accountDescriptions = distinct.Select(s => accountDescriptionsWithDups.First(d => d.AccountId == s))
				.ToDictionary(s => s.AccountId, s => s.AccountName);

			Func<InvoiceSummary, DateTime> timeBinSelector = invoice => new DateTime(invoice.InvoiceDate.Value.Year, invoice.InvoiceDate.Value.Month, 1);

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
				Sum = g.Where(o => o.TimeBin > lookFromDate).Sum(o => o.Aggregate)
			}
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
}
