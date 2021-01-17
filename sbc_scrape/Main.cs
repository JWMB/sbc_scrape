using MediusFlowAPI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using SBCScan.SBC;
using Scrape.IO;
using Scrape.IO.Selenium;
using Scrape.IO.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonTools;

namespace SBCScan
{
	class Main : IDisposable
	{
		private readonly AppSettings settings;
		private readonly ILogger<Main> logger;

		private Fetcher fetcher;
		private RemoteWebDriver driver;

		public SBCMain SBC { get; private set; }
		public MediusFlow MediusFlow { get; }

		public Main(IOptions<AppSettings> settings, IKeyValueStore store, ILogger<Main> logger)
		{
			this.settings = settings.Value;
			this.logger = logger;

			MediusFlow = new MediusFlow(store, logger); //TODO: better pattern for loggers...
		}

		public async Task Init()
		{
			driver = SetupDriver();
			logger.LogInformation($"driver.SessionId = {driver.SessionId}");

			fetcher = new Fetcher(driver, new FileSystemKVStore(settings.StorageFolderDownloadedFiles, extension: ""));

			SBC = new SBCMain(driver, fetcher);

			await SBC.Login(settings.LoginPage_BankId, settings.UserLoginId_BankId, settings.UserLogin_BrfId);

			SBC.LoginToMediusFlow(settings.RedirectUrlMediusFlow);
			var csrf = SBC.GetMediusFlowCSRFToken();

			MediusFlow.Init(fetcher, csrf);

			logger.LogInformation($"Logged in");
		}

		public async Task<List<InvoiceSummary>> LoadInvoices(bool includeOCRd, Action<int, int> progressCallback = null)
		{
			var pathToOCRed = GlobalSettings.AppSettings.StorageFolderDownloadedFiles;
			var ocrFiles = new DirectoryInfo(pathToOCRed).GetFiles("*.txt");

			var mediusToLoad = await MediusFlow.GetAvailableInvoiceFiles();
			var processIndex = 0;
			var fromMediusFlow = await MediusFlow.LoadAndTransformInvoices(CreateSummary).ToListAsync();

			var fromSbc = new List<InvoiceSummary>();
			processIndex = 0;
			await foreach(var sbcRows in new sbc_scrape.SBC.InvoiceSource().ReadAllAsync(GlobalSettings.AppSettings.StorageFolderSbcHtml))
			{
				progressCallback?.Invoke(++processIndex, processIndex);
				fromSbc.AddRange(sbcRows.Select(o => o.ToSummary()));
			}

			// TODO: SBC also has images

			var mismatched = sbc_scrape.SBC.Invoice.GetMismatchedEntries(fromMediusFlow, fromSbc);
			var tmp = string.Join("\n", mismatched.OrderByDescending(s => s.Summary.InvoiceDate)
	.Select(s => $"{(s.Summary.InvoiceDate?.ToString("yyyy-MM-dd"))} {s.Type} {s.Source} {s.Summary.AccountId} {s.Summary.Supplier} {s.Summary.GrossAmount}"));

			return sbc_scrape.SBC.Invoice.Join(fromMediusFlow, fromSbc);

			InvoiceSummary CreateSummary(InvoiceFull invoice)
			{
				var summary = InvoiceSummary.Summarize(invoice);
				if (includeOCRd)
				{
					var found = summary.InvoiceImageIds?.Select(v => new { Guid = v, File = ocrFiles.SingleOrDefault(f => f.Name.Contains(v)) })
		.Where(f => f.File != null).Select(f => new { f.Guid, Content = File.ReadAllText(f.File.FullName) });
					if (found.Any())
					{
						summary.InvoiceTexts = string.Join("\n", found.Select(f =>
							$"{f.Guid}: {string.Join('\n', f.Content.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))}")
							);
					}
				}
				progressCallback?.Invoke(++processIndex, mediusToLoad.Count);
				return summary;
			}
		}
		
		static RemoteWebDriver SetupDriver()
		{
			var options = new ChromeOptions();
			//options.ToCapabilities().HasCapability();


			var service = ChromeDriverService.CreateDefaultService(AppDomain.CurrentDomain.BaseDirectory);

			// https://app.quicktype.io/#l=cs&r=json2csharp

			return new ChromeDriver(service, options);
		}

		private void SetupDownloads(ChromeOptions options, string downloadFolder)
		{
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

		}

		public void Dispose()
		{
			driver?.Close();
			driver = null;
		}
	}
}
