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
	public class MediusFlow
	{
		private Fetcher fetcher;
		private readonly InvoiceStore store;
		private readonly ILogger logger;

		public MediusFlow(IKeyValueStore store, ILogger logger)
		{
			this.store = new InvoiceStore(store);
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

		public async Task<Dictionary<InvoiceFull, List<string>>> DownloadPdfs(Func<InvoiceFull.FilenameFormat, bool> invoiceFilter)
		{
			var invoices = await LoadInvoices(invoiceFilter);

			var api = CreateApi();
			var result = new Dictionary<InvoiceFull, List<string>>();
			foreach (var invoice in invoices)
			{
				var images = await api.GetTaskPdf(invoice.FirstTask?.Task.Document);
				foreach (var item in images)
					await fetcher.Store.Post($"{item.Key}.pdf", item.Value);
				result.Add(invoice, images.Keys.Select(k => k.ToString()).ToList());
			}
			return result;
		}

		public async Task<Dictionary<InvoiceFull, List<string>>> DownloadImages(Func<InvoiceFull.FilenameFormat, bool> invoiceFilter)
		{
			var invoices = await LoadInvoices(invoiceFilter);
			return await DownloadImages(invoices);
		}

		private async Task<Dictionary<InvoiceFull, List<string>>> DownloadImages(IEnumerable<InvoiceFull> invoices)
		{
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

		public async Task<(DateTime start, DateTime end)> GetStartEndDates(IEnumerable<InvoiceFull.FilenameFormat> alreadyScraped, DateTime? minDate, DateTime? maxDate, bool goBackwards)
		{
			var alreadyScrapedDates = alreadyScraped.Select(s => s.InvoiceDate);

			var earliestAvailableDate = new DateTime(2016, 1, 1); //TODO: should be a setting, depends on SBC/MediusFlow policies
			var dateBounds = (Min: minDate ?? earliestAvailableDate, Max: maxDate ?? DateTime.Now.Date);
			var latest = alreadyScrapedDates.Concat(new DateTime[] { dateBounds.Min }).Max();
			var earliest = alreadyScrapedDates.Concat(new DateTime[] { dateBounds.Max }).Min();

			logger?.LogInformation($"Already downloaded invoice dates: {earliest.ToShortDateString()} - {latest.ToShortDateString()}");

			var start = minDate ?? dateBounds.Min;
			var end = maxDate ?? dateBounds.Max;
			if (minDate == null || maxDate == null)
			{
				if (goBackwards)
				{
					//Normally only used first time
					//TODO: if we use it backwards for periods where we already have data, we need similar functionality to below for going forward
					start = earliest; //.Add(timespan);
					//end = earliest;
				}
				else
				{
					start = alreadyScraped.Max(iv => iv.InvoiceDate);

					//We need to revisit invoices - those with TaskState = 1 were not finished
					var nonFinished = alreadyScraped.Where(iv => iv.State == 1);
					//Not true, 1 just means not payed - can also be rejected
					//TODO: change the "State" property to reflect this
					var nonFinishedFull = (await LoadInvoices(f => nonFinished.Any(o => o.ToString() == f.ToString())));
					var nonRejected = nonFinishedFull.Where(o => !o.IsRejected).Select(o => InvoiceSummary.Summarize(o)).ToList();

					if (nonRejected.Any())
					{
						var earliestNonRejected = nonRejected.Min(iv => iv.InvoiceDate.Value);
						if (start > earliestNonRejected)
							start = earliestNonRejected;
					}
					//Also comments may have been updated, so add an additional 15 days back
					start = start.AddDays(-15);
					//end = start.Add(timespan);
				}
			}
			return (start, end);
		}

		public async Task<List<InvoiceSummary>> Scrape(DateTime? minDate = null, DateTime? maxDate = null, bool saveToDisk = true, bool goBackwards = false)
		{
			var maxNumPeriods = 26;
			//var skipAlreadyArchived = true;

			var alreadyScraped = await store.GetKeysParsed();
			var alreadyScrapedIds = alreadyScraped.Select(s => s.Id).ToList();

			var startEnd = await GetStartEndDates(alreadyScraped, minDate, maxDate, goBackwards);
			var dateBounds = (Min: startEnd.start, Max: startEnd.end);
			var (start, end) = startEnd;
			var timespan = TimeSpan.FromDays(7 * (goBackwards ? -1 : 1));
			if (goBackwards)
				start = start.Add(timespan);

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
									//TODO: would be interesting to see if there's a diff (and if so what)
									await store.Delete(oldOne);
								}
								await store.Post(item);
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
			var all = await store.GetKeysParsed();
			return quickFilter != null ? all.Where(o => quickFilter(o)).ToList() : all;
		}
		public async IAsyncEnumerable<T> LoadAndTransformInvoices<T>(Func<InvoiceFull, T> selector, Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			var files = await store.GetKeysParsed();
			foreach (var file in files)
			{
				if (quickFilter != null && quickFilter(file) == false)
					continue;
				InvoiceFull invoice = null;
				try
				{
					invoice = await store.Get(file); // JsonConvert.DeserializeObject<InvoiceFull>((await store.Get(file)).ToString());
				}
				catch (Exception ex)
				{
					throw new Exception($"Deserialization of {file} failed", ex);
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

	public class InvoiceStore
	{
		private readonly KeyValueStoreOfT<InvoiceFull> store;
		public InvoiceStore(IKeyValueStore store)
		{
			this.store = new KeyValueStoreOfT<InvoiceFull>(store,
o => JsonConvert.SerializeObject(o, Formatting.Indented),
o => JsonConvert.DeserializeObject<InvoiceFull>(o));
		}

		public async Task Post(InvoiceFull item) => await store.Post(InvoiceFull.FilenameFormat.Create(item), item);
		public async Task Delete(string key) => await store.Delete(key);
		public async Task Delete(InvoiceFull.FilenameFormat key) => await store.Delete(key.ToString());

		public async Task<InvoiceFull> Get(InvoiceFull.FilenameFormat key) => await store.Get(key.ToString());

		public async Task<List<InvoiceFull.FilenameFormat>> GetKeysParsed() =>
			(await store.GetAllKeys()).Select(k => InvoiceFull.FilenameFormat.Parse(k)).ToList();
	}
}
