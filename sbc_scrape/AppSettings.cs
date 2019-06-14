using Newtonsoft.Json;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace SBCScan
{
	public class AppSettings
	{
		public string MediusRequestHeader_XUserContext { get; set; }
		public string UserLoginId_BankId { get; set; }
		public string UserLogin_BrfId { get; set; }
		public string RedirectUrlMediusFlow { get; set; }
		public string LoginPage_BankId { get; set; }

		public string OutputFolder { get; set; }
		public string MediusFlowRoot { get; set; }
		public string StorageFolderRoot { get; set; }
		public string StorageFolderDownloadedFiles { get; set; }
		public string PathToTesseract { get; set; }
		public string StorageFolderSbcHtml { get; set; }


		public string MixedUpAccountIds { get; set; }
		public List<List<long>> MixedUpAccountIdsParsed => JsonConvert.DeserializeObject<List<List<long>>>(MixedUpAccountIds);

		public void ResolvePaths()
		{
			OutputFolder = PathExtensions.Parse(OutputFolder);
			StorageFolderRoot = PathExtensions.Parse(StorageFolderRoot);
			StorageFolderDownloadedFiles = PathExtensions.Parse(StorageFolderDownloadedFiles);
			PathToTesseract = PathExtensions.Parse(PathToTesseract);
			StorageFolderSbcHtml = PathExtensions.Parse(StorageFolderSbcHtml);
		}
	}

	public static class GlobalSettings
	{
		public static Microsoft.Extensions.Configuration.IConfiguration Configuration;
		public static AppSettings AppSettings;
	}
}
