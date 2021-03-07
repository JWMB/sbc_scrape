using MediusFlowAPI;
using Newtonsoft.Json;
using REPL;
using SBCScan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{

	class GetTaskCmd : Command
	{
		private readonly API api;
		public GetTaskCmd(API api) => this.api = api;
		public override string Id => "gettask";
		public async Task<string> Evaluate(long id)
		{
			return JsonConvert.SerializeObject(await api.GetTask(id));
		}
	}

	class ConvertInvoiceImageFilenameCmd : Command
	{
		private readonly Main main;
		public ConvertInvoiceImageFilenameCmd(Main main) => this.main = main;
		public override string Id => "convii";
		public async Task<string> Evaluate()
		{
			Console.WriteLine("0%");
			var summaries = await main.MediusFlow.LoadAndTransformInvoicesCallback(o => InvoiceSummary.Summarize(o),
				(index, total) => { if (index % 10 == 0) { Console.RewriteLine($"{100 * index / total}%"); } });

			var dir = new DirectoryInfo(GlobalSettings.AppSettings.StorageFolderDownloadedFiles);
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
		public async Task<object> Evaluate(long id)
		{
			return JsonConvert.SerializeObject(await scraper.GetInvoice(id));
		}
	}
}
