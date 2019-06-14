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

namespace SBCScan
{
	class Main : IDisposable
	{
		private readonly AppSettings settings;
		private readonly IKeyValueStore store;
		private readonly ILogger<Main> logger;

		private Fetcher fetcher;
		private SBCMain sbc;
		private MediusFlow mediusFlow;
		private RemoteWebDriver driver;
		private string downloadFolder;

		public SBCMain SBC { get => sbc; }
		public MediusFlow MediusFlow { get => mediusFlow; }

		public Main(IOptions<AppSettings> settings, IKeyValueStore store, ILogger<Main> logger)
		{
			this.settings = settings.Value;
			this.store = store;
			this.logger = logger;

			mediusFlow = new MediusFlow(store, logger); //TODO: better pattern for loggers...
		}

		public async Task Init()
		{
			driver = SetupDriver(downloadFolder);
			logger.LogInformation($"driver.SessionId = {driver.SessionId}");

			fetcher = new Fetcher(driver, new FileSystemKVStore(settings.StorageFolderDownloadedFiles, extension: ""));

			sbc = new SBCMain(driver);

			await sbc.Login(settings.LoginPage_BankId, settings.UserLoginId_BankId, settings.UserLogin_BrfId);

			mediusFlow.Init(fetcher);


			sbc.LoginToMediusFlow(settings.RedirectUrlMediusFlow);
			logger.LogInformation($"Logged in");
		}

		public async Task<List<InvoiceSummary>> LoadInvoices(bool includeOCRd)
		{
			var sbc = sbc_scrape.SBC.Invoice.ReadAll(GlobalSettings.AppSettings.StorageFolderSbcHtml)
				.Select(o => o.ToSummary()).ToList();
			var mediusFlow = await MediusFlow.LoadInvoiceSummaries(ff => ff.InvoiceDate > new DateTime(2001, 1, 1));

			if (includeOCRd)
			{
				var pathToOCRed = GlobalSettings.AppSettings.StorageFolderDownloadedFiles;
				var ocrFiles = new DirectoryInfo(pathToOCRed).GetFiles("*.txt");
				foreach (var summary in mediusFlow)
				{
					var found = summary.InvoiceImageIds?.Select(v => new { Guid = v, File = ocrFiles.SingleOrDefault(f => f.Name.Contains(v)) })
						.Where(f => f.File != null).Select(f => new { Guid = f.Guid, Content = File.ReadAllText(f.File.FullName) });
					summary.InvoiceTexts = string.Join("\n", found.Select(f =>
						$"{f.Guid}: {string.Join('\n', f.Content.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))}")
						);
				}
				// TODO: SBC also has images
			}

			var mismatched = sbc_scrape.SBC.Invoice.GetMismatchedEntries(mediusFlow, sbc);
			var tmp = string.Join("\n", mismatched.OrderByDescending(s => s.Summary.InvoiceDate)
	.Select(s => $"{(s.Summary.InvoiceDate?.ToString("yyyy-MM-dd"))} {s.Type} {s.Source} {s.Summary.AccountId} {s.Summary.Supplier} {s.Summary.GrossAmount}"));

			return sbc_scrape.SBC.Invoice.Join(mediusFlow, sbc);
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
}
