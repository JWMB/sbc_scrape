using MediusFlowAPI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using SBCScan.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SBCScan
{
	class Main : IDisposable
	{
		private readonly AppSettings settings;
		private readonly IDocumentStore store;
		private readonly ILogger<Main> logger;

		private RemoteWebDriver driver;
		private string downloadFolder;

		public Main(IOptions<AppSettings> settings, IDocumentStore store, ILogger<Main> logger)
		{
			this.settings = settings.Value;
			this.store = store;
			this.logger = logger;

			downloadFolder = PathExtensions.Parse(this.settings.StorageFolderDownloadedFiles);
		}

		public async Task Init()
		{
			driver = SetupDriver(downloadFolder);
			logger.LogInformation($"driver.SessionId = {driver.SessionId}");

			var sbc = new Sbc(driver);
			await sbc.Login(settings.LoginPage_BankId, settings.UserLoginId_BankId);

			sbc.LoginToMediusFlow(settings.RedirectUrlMediusFlow);
			logger.LogInformation($"Logged in");
		}

		public API CreateApi()
		{
			var fetcher = new Fetcher(driver, downloadFolder);
			return new API(fetcher, settings.MediusFlowRoot, settings.MediusRequestHeader_XUserContext);
		}

		public InvoiceScraper CreateScraper()
		{
			var fetcher = new Fetcher(driver, downloadFolder);

			//TODO: can we get the x-user-context header from existing requests? E.g. puppeteers Network.responseReceived
			return new InvoiceScraper(fetcher, settings.MediusFlowRoot, settings.MediusRequestHeader_XUserContext);
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

			DateTime start = latest;
			DateTime end = latest.Add(timespan);

			if (goBackwards)
			{
				start = earliest.Add(timespan);
				end = earliest;
			}

			var skippedIds = new List<long>();
			var scraper = CreateScraper();
			var result = new List<InvoiceSummary>();
			for (int i = 0; i < maxNumPeriods; i++)
			{
				MediusFlowAPI.Models.SupplierInvoiceGadgetData.Response gadgetData;
				try
				{
					gadgetData = await scraper.GetSupplierInvoiceGadgetData(
						start > dateBounds.Min ? start : dateBounds.Min,
						end < dateBounds.Max ? end : dateBounds.Max);
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

		public async Task<List<T>> LoadAndTransformInvoices<T>(Func<InvoiceFull.FilenameFormat, bool> quickFilter = null, Func<InvoiceFull, T> selector = null)
		{
			var files = (await store.GetAllKeys()).ToList();
			var result = new List<T>();
			foreach (var file in files)
			{
				if (quickFilter != null && quickFilter(InvoiceFull.FilenameFormat.Parse(file)) == false)
					continue;
				var invoice = JsonConvert.DeserializeObject<InvoiceFull>((await store.Get(file)).ToString());
				result.Add(selector(invoice));
			}
			return result;
		}
		public async Task<List<InvoiceSummary>> LoadInvoiceSummaries(Func<InvoiceFull.FilenameFormat, bool> quickFilter = null)
		{
			return await LoadAndTransformInvoices(quickFilter: quickFilter, selector: invoice => InvoiceSummary.Summarize(invoice));
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
			Func<InvoiceSummary, TGroupBy> groupSelector, TimeSpan bin)
		{
			var tsAsTicks = bin.Ticks;
			var summed = summaries.
				Select(o => new { O = o, Bin = new DateTime((o.InvoiceDate.Value.Ticks / tsAsTicks) * tsAsTicks) })
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

		public async Task<string> CreateIndex()
		{
			var byDate = (await LoadInvoiceSummaries()).OrderByDescending(r => r.InvoiceDate).ToList();
			//var xxx = JsonConvert.SerializeObject(byDate, Formatting.Indented);
			return ServiceStack.Text.CsvSerializer.SerializeToString(byDate);
		}

		static RemoteWebDriver SetupDriver(string downloadFolder)
		{
			var options = new ChromeOptions();
			//options.ToCapabilities().HasCapability();

			if (!string.IsNullOrEmpty(downloadFolder) && !Directory.Exists(downloadFolder))
				Directory.CreateDirectory(downloadFolder);

			var settings = new Dictionary<string, object> {
				{ "browser.download.folderList", 2 },
				{ "browser.helperApps.neverAsk.saveToDisk", "image/jpg, image/png, application/pdf" },
				{ "browser.download.dir", downloadFolder },
				{ "browser.download.useDownloadDir", true },
				//{ "pdfjs.disabled", true },  // disable the built-in PDF viewer
			};
			foreach (var kv in settings)
				options.AddArgument($"--{kv.Key}={kv.Value}");

			var service = ChromeDriverService.CreateDefaultService(AppDomain.CurrentDomain.BaseDirectory);

			// https://app.quicktype.io/#l=cs&r=json2csharp

			return new ChromeDriver(service, options);
		}

		public void Dispose()
		{
			driver?.Close();
			driver = null;
		}
	}

	public class Sbc
	{
		private readonly RemoteWebDriver driver;

		public Sbc(RemoteWebDriver driver)
		{
			this.driver = driver;
		}

		public async Task Login(string loginUrl, string username)
		{
			var wait10 = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

			driver.Navigate().GoToUrl(loginUrl);

			driver.WaitUntilDocumentReady();

			var pid = driver.FindElement(By.Id("login_UserName"));
			pid.Clear();
			pid.SendKeys(username);

			var btn = driver.FindElement(By.Id("login_Login_Button"));
			btn.Click();

			var finder = By.Id("Forening_picker_login_Login_select_forening_1");
			new WebDriverWait(driver, TimeSpan.FromMinutes(4)).Until(WebDriverExtensions.ElementIsPresent(finder));
			driver.FindElement(finder).Click();

			await Task.Delay(500);
			//System.Threading.Thread.Sleep(1000);
			driver.WaitUntilDocumentReady();
		}

		public void LoginToMediusFlow(string url)
		{
			// https://stackoverflow.com/questions/17547473/how-to-open-a-new-tab-using-selenium-webdriver
			driver.Navigate().GoToUrl(url);
			driver.WaitUntilDocumentReady();
		}
	}
}
