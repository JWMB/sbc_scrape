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
		public string RedirectUrlMediusFlow { get; set; }
		public string LoginPage_BankId { get; set; }

		public string MediusFlowRoot { get; set; }
		public string MediusFlowRootResolved => PathExtensions.Parse(MediusFlowRoot);

		public string StorageFolderRoot { get; set; }
		public string StorageFolderRootResolved => PathExtensions.Parse(StorageFolderRoot);

		public string StorageFolderDownloadedFiles { get; set; }
		public string StorageFolderDownloadedFilesResolved => PathExtensions.Parse(StorageFolderDownloadedFiles);

		public string MixedUpAccountIds { get; set; }
		public List<List<long>> MixedUpAccountIdsParsed => JsonConvert.DeserializeObject<List<List<long>>>(MixedUpAccountIds);
	}

	public static class GlobalSettings
	{
		public static Microsoft.Extensions.Configuration.IConfiguration Configuration;
		public static AppSettings AppSettings;
	}
}
