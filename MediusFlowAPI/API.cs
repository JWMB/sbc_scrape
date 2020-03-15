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

		public async Task<Dictionary<Guid, byte[]>> GetTaskPdf(Models.Task.Document document)
		{
			var images = new Dictionary<Guid, byte[]>();
			var imageInfos = document.HashFiles.Where(hf => hf.HashFileType == "InvoiceImage");
			foreach (var hf in imageInfos)
				images.Add(hf.Hash, await GetPdf(hf.Hash, document.Id));
			return images;
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
			var body = new { folderId, };
			var result = await Request(baseAddress + "Rpc/lightApi/InboxTaskService/GetTasks", "POST", body);
			var json = JsonConvert.SerializeObject(result.Body);
			return json.Length <= 2 ? new Models.Tasks.Response[] { } : Models.Tasks.Response.FromJson(json);
		}

		public async Task<Models.Comment.Response[]> GetComments(Guid guid)
		{
			var body = new
			{
				entityType = "Medius.ExpenseInvoice.Entities.ExpenseInvoice",
				entityViewId = guid.ToString(),
				_ = DateTime.Now.ToUnixTimestamp()
			};
			//var result = await Request(baseAddress + "Rpc/CommentsManager/GetComments", "POST", body);
			//return Models.Comment.Response.FromJson(JsonConvert.SerializeObject(result.Body));
			//Backend/Rest/comments/?entityViewId=1b48d5d8-6788-456a-9397-ab9643a5640e&entityType=Medius.ExpenseInvoice.Entities.ExpenseInvoice&_=1569073939309
			var result = await Request(baseAddress + "Rest/comments/?", "GET", body);

			var comments2 =  Models.Comment2.Response.FromJson(JsonConvert.SerializeObject(result.Body));
			return comments2.Select(o => o.ToCommentResponse()).ToArray();
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

		public async Task<byte[]> GetPdf(Guid hash, long docId)
		{
			var headers = GetHeaders(new Dictionary<string, string> {
					{ "accept", "*/*" },
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

			var response = await fetcher.Fetch(
				baseAddress + $"Rest/MediaService/image/{hash}/pdf?docId={docId}&docType=Medius.ExpenseInvoice.Entities.ExpenseInvoice&tag=DocumentImage&download=application/pdf;base64",
				config);
			return System.Text.Encoding.UTF8.GetBytes(response.Body.ToString());
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
}
