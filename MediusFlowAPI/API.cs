using MediusFlowAPI.Models;
using Newtonsoft.Json;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediusFlowAPI
{
	public class API
	{
		private readonly IFetcher fetcher;
		private readonly string baseAddress;
		private readonly string mediusRequestHeader_XUserContext;

		public API(IFetcher fetcher, string baseAddress, string mediusRequestHeader_XUserContext)
		{
			this.fetcher = fetcher;
			this.baseAddress = baseAddress;
			this.mediusRequestHeader_XUserContext = mediusRequestHeader_XUserContext;
		}

		public async Task<InvoiceFull.TaskFull> GetAllTaskInfo(long taskId, bool downloadImages = false)
		{
			var task = await GetTask(taskId);
			var history = await GetTaskHistoryForViewId(task.Document.ViewId);
			var comments = await GetComments(task.Document.ViewId);
			var images = await GetTaskImages(GetTaskImagesInfo(task), downloadImages);
			return new InvoiceFull.TaskFull { Comments = comments, Images = images, History = history, Task = task };
		}

		public IEnumerable<Models.Task.HashFile> GetTaskImagesInfo(Models.Task.Response task)
		{
			return task?.Document?.HashFiles?.Where(hf => hf.HashFileType == "InvoiceImage");
		}
		public async Task<Dictionary<Guid, object>> GetTaskImages(IEnumerable<Models.Task.HashFile> imageInfos, bool downloadImages, string filenameFormat = "{0}")
		{
			var images = new Dictionary<Guid, object>();
			if (imageInfos != null)
			{
				foreach (var hf in imageInfos)
					images.Add(hf.Hash, downloadImages ? await GetMedia(hf.Hash, string.Format(filenameFormat, hf.HashFileType)) : ""); //Pretty slow
			}
			return images;
		}

		public async Task<Models.Task.Response> GetTask(long id)
		{
			var body = new { taskId = id, };
			var result = await Request(baseAddress + "Rpc/lightApi/InboxTaskService/GetTask", "POST", body);
			return Models.Task.Response.FromJson(JsonConvert.SerializeObject(result.Body));
		}

		public async Task<List<TaskAssignment>> GetTaskAssignments(long id)
		{
			//https://webbattestxi.sbc.se/Mediusflow/Backend/Rest/InboxManager/TasksAssignments/5172882
			var result = await Request(baseAddress + $"Rest/InboxManager/TasksAssignments/{id}");
			var json = JsonConvert.SerializeObject(result.Body);
			return json.Length <= 2 ? new List<TaskAssignment>() : JsonConvert.DeserializeObject<List<TaskAssignment>>(json);
		}

		public async Task<Models.Tasks.Response[]> GetTasks(long folderId)
		{
			var body = new { folderId = folderId, };
			var result = await Request(baseAddress + "Rpc/lightApi/InboxTaskService/GetTasks", "POST", body);
			var json = JsonConvert.SerializeObject(result.Body);
			return json.Length <= 2 ? new Models.Tasks.Response[] { } : Models.Tasks.Response.FromJson(json);
		}

		public async Task<Models.Comment.Response[]> GetComments(Guid guid)
		{
			var body = new
			{
				entityType = "Medius.ExpenseInvoice.Entities.ExpenseInvoice",
				entityViewId = guid.ToString()
			};
			var result = await Request(baseAddress + "Rpc/CommentsManager/GetComments", "POST", body);
			return Models.Comment.Response.FromJson(JsonConvert.SerializeObject(result.Body));
		}

		public async Task<Models.TaskHistory.Response[]> GetTaskHistoryForViewId(Guid guid)
		{
			var body = new {
				entityType = "Medius.ExpenseInvoice.Entities.ExpenseInvoice",
				entityViewId = guid.ToString()
			};
			var result = await Request(baseAddress + "Rpc/WorkflowHistoryManager/GetTaskHistoryForViewId", "POST", body);
			return Models.TaskHistory.Response.FromJson(JsonConvert.SerializeObject(result.Body));
		}

		public async Task<Models.SupplierInvoiceGadgetData.Response> GetSupplierInvoiceGadgetData(DateTime from, DateTime to, int page = 0, int take = 100)
		{
			return await GetSupplierInvoiceGadgetData(new Models.SupplierInvoiceGadgetData.Filters
			{
				//CompanyHierarchyId = "", ///1/4/5/2404/",
				InvoiceDateFrom = from.ToUtcString(),
				InvoiceDateTo = to.ToUtcString(),
			}, page, take);
		}
		public async Task<Models.SupplierInvoiceGadgetData.Response> GetSupplierInvoiceGadgetData(Models.SupplierInvoiceGadgetData.Filters filters, int page = 0, int take = 100)
		{
			var body = new Models.SupplierInvoiceGadgetData.Request
			{
				Filters = filters,
				PageSize = take,
				ActualPage = page + 1
			};

			var result = await Request(baseAddress + "Rpc/PurchaseToPayGadgetDataService/GetSupplierInvoiceGadgetData", "POST", body);
			var typed = Models.SupplierInvoiceGadgetData.Response.FromJson(JsonConvert.SerializeObject(result.Body));
			return typed;
		}


		public async Task<Models.AccountingObjectWithLinesForInvoice.Response> GetAccountingObjectWithLinesForInvoice(long invoiceId)
		{
			var body = new Models.AccountingObjectWithLinesForInvoice.Request {
				DocumentContext = new Models.AccountingObjectWithLinesForInvoice.DocumentContext {
					WorkflowStepName = "ApproveDocument",
					DocumentTypeName = "Medius.ExpenseInvoice.Entities.ExpenseInvoice",
					DocumentId = invoiceId,
				},
				InvoiceId = invoiceId,
			};
			var result = await Request(baseAddress + "Rpc/lightApi/AccountingService/GetAccountingObjectWithLinesForInvoice", "POST", body);
			var typed = Models.AccountingObjectWithLinesForInvoice.Response.FromJson(JsonConvert.SerializeObject(result.Body));
			return typed;

		}

		public async Task<object> GetMedia(Guid hash, string tag)
		{
			var headers = GetHeaders(new Dictionary<string, string> {
					{ "accept", "image/webp,image/apng,image/*,*/*;q=0.8" },
					{ "accept-encoding", "gzip, deflate, br" },
					{ "cache-control", "no-cache" }
			});
			var config = new FetchConfig
			{
				Method = MethodMode.Get,
				Mode = CorsMode.Cors,
				Headers = headers,
				Credentials = CredentialsMode.Include,
				ReferrerPolicy = ReferrerPolicyMode.NoReferrerWhenDowngrade
			};
			return await fetcher.DownloadFile(baseAddress + $"Rest/MediaService/image/{hash}/png/pages/1?tag={tag}",
				config, $"{hash}.png");
		}

		public async Task<FetchResponse> Request(string url, string method = "GET", object body = null, Dictionary<string, string> headerOverrides = null)
		{
			if (headerOverrides == null)
				headerOverrides = new Dictionary<string, string>();
			headerOverrides["x-user-context"] = mediusRequestHeader_XUserContext;
			return await Request(fetcher, url, method, body, headerOverrides);
		}

		public static Dictionary<string, string> GetHeaders(Dictionary<string, string> headerOverrides = null)
		{
			//"referrer": "https://webbattestxi.sbc.se/Mediusflow/SbcPortal",
			var headers = new Dictionary<string, string> {
				{ "accept", "application/json, text/javascript, */*; q=0.01" },
				{ "accept-language", "en-US,en;q=0.9"},
				{ "content-type", "application/json; charset=UTF-8" },
				{ "x-installed-applications", "Archive/11.28.4.0;BaseApplication/0.0.0.0;Core/11.28.4.0;Cst_ExpenseInvoice/11.28.4.6;Enterprise/11.28.4.0;ExpenseInvoice/11.28.4.0;PurchaseToPay/11.28.4.0;SbcCore/11.28.4.2;SbcCoreWeb/11.28.4.2;Security/11.28.4.0;WorkflowStudio/11.28.4.0" },
			};
			if (headerOverrides != null)
				foreach (var kv in headerOverrides)
					headers[kv.Key] = kv.Value;
			return headers;
		}
		public async static Task<FetchResponse> Request(IFetcher fetch, string url, string method = "GET", object body = null, Dictionary<string, string> headerOverrides = null)
		{
			var headers = GetHeaders(headerOverrides);
			return await fetch.Fetch(url, new FetchConfig { Method = MethodMode.Parse(method),
				Body = body, Mode = CorsMode.Cors, Headers = headers,
				Credentials = CredentialsMode.Include, ReferrerPolicy = ReferrerPolicyMode.NoReferrerWhenDowngrade });
		}

	}

	public class InvoiceSummary
	{
		public long Id { get; set; }
		public long? TaskId { get; set; }
		public DateTime? InvoiceDate { get; set; }
		public DateTime? CreatedDate { get; set; }
		public DateTime? DueDate { get; set; }
		public long? TaskState { get; set; }
		public decimal GrossAmount { get; set; }
		public string Supplier { get; set; }
		public long? AccountId { get; set; }
		public string AccountName { get; set; }
		public double? VAT { get; set; }
		//public string InvoiceType { get; set; }
		public string Houses
		{
			get
			{
				return string.Join(", ",
					RxFindHouse.Select(rx => rx.Matches(Comments))
					.Where(ms => ms.Count > 0).SelectMany(ms => ms.Cast<Match>().ToList()).Select(m => m.Groups["house"].Value)
					.Distinct().OrderBy(s => s));
			}
		}
		static List<Regex> _rxFindHouse;
		static List<Regex> RxFindHouse
		{
			get
			{
				if (_rxFindHouse == null)
					_rxFindHouse = new List<Regex> {
						new Regex(@"(Riksrådsv(ägen|\.)?|RRV|rrv|(n|N)r)\s?(?<house>\d{2,3})"),
						new Regex(@"(?<house>\d{2,3})\:an"),
					};
				return _rxFindHouse;
			}
		}

		public string Comments { get; set; }
		public string History { get; set; }



		static System.Text.RegularExpressions.Regex rxSimplifyAuthor =
			new System.Text.RegularExpressions.Regex(@"(?<name>(\w+\s){1,2})\s?\((\d{5,6}|SYSTEM)\)");
		public static InvoiceSummary Summarize(InvoiceFull invoice)
		{
			var iv = invoice.Invoice;
			var ft = invoice.FirstTask;
			var task = ft?.Task;
			var taskDoc = task?.Document;
			var accountingDimension1 = invoice.Accounting?.DimensionStrings?.FirstOrDefault().Dimensions?.Dimension1;

			if (iv.FinalBookingDate.FromMediusDate() != iv.InvoiceDate.FromMediusDate())
			{ }
			if (iv.ActualPaymentDate.FromMediusDate() != null)
			{ }
			//if (taskDoc?.CustomFields?.TextCustomField1 != "382" && !string.IsNullOrEmpty(taskDoc?.CustomFields?.TextCustomField1))
			//{ }

			string SimplifyAuthor(string author) => rxSimplifyAuthor.Replace(author, m => m.Groups["name"].Value.Trim()).Trim();
			string GetHistoryItemSummary(Models.TaskHistory.Response item)
			{
				var description = SimplifyAuthor(item.Description);
				var replacements = new Dictionary<string, string> {
					{ "Fakturan attesterades av", "Attest:" },
					{ "Svara utfördes av", "Svar:" },
					{ "Granskning skickad till", "Att granska" },
					{ "Granskning slutförd av", "Granskad:" },
					{ "Distribuera utfördes av", "Dist:" },
					{ "Granskning återtagen av", "Ogranskad:" }
				};
				foreach (var kv in replacements)
					description = description.Replace(kv.Key, kv.Value);
				return $"{item.Date?.FromMediusDate()?.ToString("MM-dd")}: {SimplifyAuthor(description)}";
			}
			string GetCommentSummary(Models.Comment.Response c)
			{
				return $"{SimplifyAuthor(c.Author)} ({c.CreatedDate?.FromMediusDate()?.ToString("MM-dd HH:mm")}):{c.Text.Replace("\n", "")}";
			}
			string Join(string separator, IEnumerable<string> strings) => strings == null ? "" : string.Join(separator, strings);
			string RemoveFromEnd(string str, string toRemove) => str.EndsWith(toRemove) ? str.Remove(str.Length - toRemove.Length) : str;

			try
			{
				return new InvoiceSummary
				{
					Id = iv.Id,
					InvoiceDate = iv.InvoiceDate.FromMediusDate(),
					CreatedDate = taskDoc?.CreatedTimestamp.FromMediusDate()?.Date,
					TaskState = task?.State,
					TaskId = task?.Id,
					GrossAmount = decimal.Parse(iv.GrossAmount.DisplayValue),
					Supplier = iv.Supplier.Name,
					DueDate = iv.DueDate.FromMediusDate(),
					AccountId = accountingDimension1?.Value?.ValueValue,
					AccountName = RemoveFromEnd(accountingDimension1?.Value?.Description, " -E-"),
					VAT = taskDoc?.CustomFields.NumericCustomField1.Value,
					Comments = Join(",", ft?.Comments?.Select(c => GetCommentSummary(c))),
					History = Join(",", ft?.History?.Select(c => GetHistoryItemSummary(c))),
				};
			}
			catch (Exception ex)
			{
				return null;
			}
		}
	}

	public class InvoiceFull
	{
		public class TaskAssignmentAndTasks
		{
			public Models.TaskAssignment TaskAssignment { get; set; }
			public TaskFull Task { get; set; }
		}
		public class TaskFull
		{
			public Models.Task.Response Task { get; set; }
			public Models.TaskHistory.Response[] History { get; set; }
			public Models.Comment.Response[] Comments { get; set; }
			public Dictionary<Guid, object> Images { get; set; }
		}
		public Models.SupplierInvoiceGadgetData.Invoice Invoice { get; set; }
		public Models.AccountingObjectWithLinesForInvoice.Response Accounting { get; set; }
		public List<TaskAssignmentAndTasks> TaskAssignments { get; set; } = new List<InvoiceFull.TaskAssignmentAndTasks>();


		[JsonIgnore()]
		public TaskFull FirstTask { get => TaskAssignments?.FirstOrDefault()?.Task; }

		public override string ToString()
		{
			return $"{(Invoice.InvoiceDate.FromMediusDate()?.ToString("yyyy-MM-dd") ?? "0000")} {Invoice.Supplier.Name} {Invoice.GrossAmount.DisplayValue} {Invoice.Id}";
		}

		static string MakeSafeFilename(string str, int truncate = 0)
		{
			foreach (char c in System.IO.Path.GetInvalidFileNameChars())
				if (str.Contains(c))
					str = str.Replace(c, '_');
			return truncate > 0 && str.Length > truncate ? str.Remove(truncate) : str;
		}

		public static string GetFilenamePrefix(DateTime invoiceDate, string supplier, long invoiceId)
		{
			return string.Join("_",
				invoiceDate.ToString("yyyy-MM-dd"),
				MakeSafeFilename(supplier, 15),
				invoiceId.ToString());
		}

		public class FilenameFormat
		{
			public DateTime InvoiceDate { get; set; }
			public long Id { get; set; }
			public string Supplier { get; set; }
			public DateTime RegisteredDate { get; set; }
			public long? State { get; set; }

			public override string ToString()
			{
				return GetFilenamePrefix(InvoiceDate, Supplier, Id) + string.Join("_",
					RegisteredDate.ToString("MM-dd"),
					State.ToString()
					);
			}
			public static string Create(InvoiceFull invoice)
			{
				var registeredDate = invoice.TaskAssignments.Min(ta => ta.Task.Task.CreatedTimestamp.FromMediusDate());
				//TODO: key should be the registered date?
				var fmt = new FilenameFormat {
					InvoiceDate = invoice.Invoice.InvoiceDate.FromMediusDate() ?? DateTime.MinValue,
					Supplier = invoice.Invoice.Supplier.Name,
					Id = invoice.Invoice.Id,
					RegisteredDate = registeredDate ?? DateTime.MinValue,
					State = invoice.FirstTask?.Task?.State
				};

				return fmt.ToString();
			}
			public static FilenameFormat Parse(string filename)
			{
				var split = filename.Split('_');
				var invoiceDate = DateTime.Parse(split[0]);
				var status = 0;
				var lastIndex = split.Length - 1;
				if (split.Length > 4)
				{
					status = int.Parse(split[lastIndex]);
					lastIndex--;
				}
				var registered = split[lastIndex];
				var rMonth = int.Parse(registered.Split('-')[0]);
				var registeredDate = DateTime.Parse("" + (rMonth < invoiceDate.Month ? invoiceDate.Year + 1 : invoiceDate.Year)
					+ "-" + registered);

				return new FilenameFormat {
					InvoiceDate = invoiceDate,
					Supplier = split[1],
					Id = long.Parse(split[lastIndex - 1]),
					RegisteredDate = registeredDate,
					State = status,
				};
			}
		}
	}
}
