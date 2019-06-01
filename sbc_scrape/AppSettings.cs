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
		public string StorageFolderRoot { get; set; }
		public string StorageFolderDownloadedFiles { get; set; }
	}
}
