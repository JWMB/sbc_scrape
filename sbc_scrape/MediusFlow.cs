using MediusFlowAPI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scrape.IO.Selenium;
using Scrape.IO.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan
{
	class MediusFlow
	{
		private Fetcher fetcher;
		private readonly IKeyValueStore store;
		private readonly ILogger logger;

		public MediusFlow(IKeyValueStore store, ILogger logger)
		{
			this.store = store;
			this.logger = logger;
		}

		public void Init(Fetcher fetcher)
		{
			this.fetcher = fetcher;
		}

		public API CreateApi()
		{
			return new API(fetcher, GlobalSettings.AppSettings.MediusFlowRoot, GlobalSettings.AppSettings.MediusRequestHeader_XUserContext);
		}

		public InvoiceScraper CreateScraper()
		{
			//TODO: can we get the x-user-context header from existing requests? E.g. puppeteers Network.responseReceived
			return new InvoiceScraper(fetcher, GlobalSettings.AppSettings.MediusFlowRoot, GlobalSettings.AppSettings.MediusRequestHeader_XUserContext);
		}

		public async Task<Dictionary<InvoiceFull, List<string>>> DownloadImages(DateTime from, DateTime? to = null) //IEnumerable<long> invoiceIds)
		{
			if (to == null)
				to = DateTime.Now;
			var invoices = await LoadInvoices(ff => ff.InvoiceDate >= from && ff.InvoiceDate <= to);
			var api = CreateApi();
			var result = new Dictionary<InvoiceFull, List<string>>();
			foreach (var invoice in invoices)
			{
				var filenameFormat = InvoiceFull.GetFilenamePrefix(invoice.Invoice.InvoiceDate.FromMediusDate().Value,
					invoice.Invoice.Supplier.Name, invoice.Invoice.Id) + "_{0}";
				var images = await api.GetTaskImages(api.GetTaskImagesInfo(invoice.FirstTask?.Task), true, filenameFormat);
				result.Add(invoice, images.Keys.Select(k => k.ToString()).ToList());
			}
			return result;
		}

		public async Task<List<InvoiceSummary>> Scrape(DateTime? minDate = null, DateTime? maxDate = null, bool saveToDisk = true, bool goBackwards = false)
		{
			var skipAlreadyArchived = true;
			var alreadyScraped = (await store.GetAllKeys()).Select(k => InvoiceFull.FilenameFormat.Parse(k)).ToList(); // .GetAllArchivedInvoiceDates();
			var alreadyScrapedDates = alreadyScraped.Select(s => s.InvoiceDate);
			var alreadyScrapedIds = alreadyScraped.Select(s => s.Id).ToList();

			var dateBounds = (Min: minDate ?? new DateTime(2016, 1, 1), Max: maxDate ?? DateTime.Now.Date);
			var latest = alreadyScrapedDates.Concat(new DateTime[] { dateBounds.Min }).Max();
			var earliest = alreadyScrapedDates.Concat(new DateTime[] { dateBounds.Max }).Min();

			logger.LogInformation($"Already downloaded invoice dates: {earliest.ToShortDateString()} - {latest.ToShortDateString()}");

			var timespan = TimeSpan.FromDays(7 * (goBackwards ? -1 : 1));
			var maxNumPeriods = 26;

			DateTime start, end;
			if (goBackwards)
			{
				start = earliest.Add(timespan);
				end = earliest;
			}
			else
			{
				//We need to revisit invoices - those with TaskState = 1 were not finished
				//Also comments may have been updated, so add an additional 15 days back
				start = alreadyScraped.Where(iv => iv.State == 1).Min(iv => iv.InvoiceDate).AddDays(-15);
				end = start.Add(timespan);
			}

			var skippedIds = new List<long>();
			var scraper = CreateScraper();
			var result = new List<InvoiceSummary>();
			for (int i = 0; i < maxNumPeriods; i++)
			{
				MediusFlowAPI.Models.SupplierInvoiceGadgetData.Response gadgetData;
				var dateRange = (Start: start > dateBounds.Min ? start : dateBounds.Min,
					End: end < dateBounds.Max ? end : dateBounds.Max);
				try
				{
					logger.LogInformation($"GetSupplierInvoiceGadgetData {dateRange.Start} - {dateRange.End}");
					gadgetData = await scraper.GetSupplierInvoiceGadgetData(dateRange.Start, dateRange.End);
				}
				catch (Exception ex)
				{
					logger.LogCritical(ex, $"GetSupplierInvoiceGadgetData");
					break;
				}

				logger.LogInformation($"Found {gadgetData.Invoices.Count()} invoices");

				var ordered = goBackwards ?
					gadgetData.Invoices.OrderByDescending(iv => (iv.InvoiceDate?.FromMediusDate() ?? DateTime.Today))
					: gadgetData.Invoices.OrderBy(iv => (iv.InvoiceDate?.FromMediusDate() ?? DateTime.Today));

				foreach (var invoice in ordered)
				{
					if (skipAlreadyArchived && alreadyScrapedIds.Contains(invoice.Id))
					{
						if (alreadyScraped.First(s => s.Id == invoice.Id).State == 2)
						{
							//TODO: we really should download, in case comments/history have changed (or include that info in filename)
							skippedIds.Add(invoice.Id);
							logger.LogInformation($"Skipping {invoice.Id} {invoice.InvoiceDate?.FromMediusDate()}");
							continue;
						}
					}

					try
					{
						var downloaded = await scraper.GetInvoices(new List<MediusFlowAPI.Models.SupplierInvoiceGadgetData.Invoice> { invoice });
						foreach (var item in downloaded)
						{
							if (saveToDisk)
							{
								if (alreadyScrapedIds.Contains(invoice.Id))
								{
									var oldOne = alreadyScraped.First(s => s.Id == invoice.Id);
									await store.Delete(oldOne.ToString());
								}
								await store.Post(InvoiceFull.FilenameFormat.Create(item), item);
							}
							var summarized = InvoiceSummary.Summarize(item);
							result.Add(summarized);
							logger.LogInformation($"{summarized.Id} {summarized.InvoiceDate} {summarized.CreatedDate} {summarized.GrossAmount} {summarized.Supplier}");
						}
					}
					catch (Exception ex)
					{
						logger.LogCritical(ex, $"GetInvoice {invoice.Id} {invoice.InvoiceDate?.FromMediusDate()}");
					}
				}

				if (start <= dateBounds.Min || (goBackwards ? end > dateBounds.Max : end >= dateBounds.Max))
					break;
				start = start.Add(timespan);
				end = end.Add(timespan);
			}
			logger.LogInformation($"Complete");
			return result;
		}

		public async Task<List<InvoiceFull>> LoadInvoices(Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			return await LoadAndTransformInvoices(o => o, quickFilter).ToListAsync();
		}

		public async Task<List<InvoiceFull.FilenameFormat>> GetAvailableInvoiceFiles(Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			var all = (await store.GetAllKeys()).Select(o => InvoiceFull.FilenameFormat.Parse(o));
			if (quickFilter != null)
				all = all.Where(o => quickFilter(o));
			return all.ToList();
		}
		public async IAsyncEnumerable<T> LoadAndTransformInvoices<T>(Func<InvoiceFull, T> selector, Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			var files = (await store.GetAllKeys()).ToList();
			foreach (var file in files)
			{
				if (quickFilter != null && quickFilter(InvoiceFull.FilenameFormat.Parse(file)) == false)
					continue;
				InvoiceFull invoice = null;
				try
				{
					invoice = JsonConvert.DeserializeObject<InvoiceFull>((await store.Get(file)).ToString());
				}
				catch (Exception ex)
				{
					throw new Exception($"Deserialization of {file} failed");
				}
				yield return selector(invoice);
			}
		}
		public async Task<List<InvoiceSummary>> LoadInvoiceSummaries(Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			return await LoadAndTransformInvoices(quickFilter: quickFilter, selector: invoice => InvoiceSummary.Summarize(invoice)).ToListAsync();
		}
		public async Task<List<T>> LoadAndTransformInvoicesCallback<T>(Func<InvoiceFull, T> selector, Action<int, int> callback, Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			var result = new List<T>();
			var toLoad = await GetAvailableInvoiceFiles(quickFilter);
			await foreach (var o in LoadAndTransformInvoices(selector))
			{
				result.Add(o);
				callback(result.Count, toLoad.Count);
			}
			return result;
		}

		public class TimeSpanAndFuncAggregate<TGroupBy, TAggregate>
		{
			public TGroupBy GroupedBy { get; set; }
			public DateTime TimeBin { get; set; }
			public List<long> InvoiceIds { get; set; }
			public TAggregate Aggregate { get; set; }

		}
		public List<TimeSpanAndFuncAggregate<TGroupBy, TAggregate>> AggregateByTimePeriodAndFunc<TGroupBy, TAggregate>(
			List<InvoiceSummary> summaries, Func<IEnumerable<InvoiceSummary>, TAggregate> aggregator,
			Func<InvoiceSummary, TGroupBy> groupSelector, Func<InvoiceSummary, DateTime> timeBinSelector)
		{
			var summed = summaries.
				Select(o => new { O = o, Bin = timeBinSelector(o) })
				.GroupBy(o => o.Bin)
				.SelectMany(g => g.GroupBy(o => groupSelector(o.O))
					.Select(byGrouping => new TimeSpanAndFuncAggregate<TGroupBy, TAggregate>
					{
						GroupedBy = byGrouping.Key,
						TimeBin = g.Key,
						Aggregate = aggregator(byGrouping.Select(ss => ss.O)),
						InvoiceIds = byGrouping.Select(ss => ss.O.Id).ToList()
					})
					.ToList()
			).OrderByDescending(o => o.TimeBin);
			return summed.ToList();
		}
	}
}
