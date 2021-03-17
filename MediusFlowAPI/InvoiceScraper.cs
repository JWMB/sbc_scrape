using Newtonsoft.Json;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediusFlowAPI
{
	public class InvoiceScraper
	{
		private readonly API api;

		public InvoiceScraper(IFetcher fetcher, string rootAddress, string xusercontext, string csrfToken)
		{
			api = new API(fetcher, rootAddress, xusercontext, csrfToken);
		}

		// "/Rpc/LinksService/GetLinks" - H "Accept: application/json, text/javascript, */*; q=0.01" - H "Content-Type: application/json; charset=utf-8" - H "X-Json-Preserve-References: true"
		// "/Rpc/AttachmentsManager/GetAttachments" -H "Accept: application/json, text/javascript, */*; q=0.01"  -H "Content-Type: application/json; charset=utf-8" -H "X-Json-Preserve-References: true"--data "{""entityViewId"":""<<GUID>>"",""entityType"":""Medius.ExpenseInvoice.Entities.ExpenseInvoice""}"
		// "/Rpc/lightApi/LabelsService/GetLabelsIdsAssignedToDocument"-H "Accept: application/json, text/javascript, */*; q=0.01" -H "Accept-Language: en-US,en;q=0.5" -H "Content-Type: application/json; charset=utf-8" -H "X-Json-Preserve-References: true" -H "Pragma: no-cache" -H "Cache-Control: no-cache" --data "{""documentId"":<<int id>>}"

		public async Task<List<InvoiceFull>> GetInvoices(IEnumerable<Models.SupplierInvoiceGadgetData.Invoice> invoiceGadgetData)
		{
			var result = new List<InvoiceFull>();
			foreach (var invoice in invoiceGadgetData)
			{
				var accounting = await api.GetAccountingObjectWithLinesForInvoice(invoice.Id);
				var invF = new InvoiceFull { Invoice = invoice, Accounting = accounting };
				result.Add(invF);
				invF.TaskAssignments = await GetTaskAssignmentAndTasks(invoice.Id);
			}
			return result;
		}

		public async Task<Models.SupplierInvoiceGadgetData.Response> GetSupplierInvoiceGadgetData(DateTime start, DateTime? end = null)
		{
			var pageIndex = 0;
			var pageSize = 100;

			Models.SupplierInvoiceGadgetData.Response result = null;
			var allInvoices = new List<Models.SupplierInvoiceGadgetData.Invoice>();
			while (true)
			{
				var invoiceResult = await api.GetSupplierInvoiceGadgetData(start, end ?? DateTime.Now.Date, pageIndex, pageSize);
				result = result ?? invoiceResult;

				allInvoices.AddRange(invoiceResult.Invoices);
				if (allInvoices.Count >= invoiceResult.RowCount || invoiceResult.Invoices.Count() < pageSize)
					break;
				pageIndex++;
			}
			result.Invoices = allInvoices.ToArray();
			result.RowCount = allInvoices.Count;
			return result;
		}

		public async Task<InvoiceFull> GetInvoice(long invoiceId)
		{
			var invoiceResult = await api.GetSupplierInvoiceGadgetData(new Models.SupplierInvoiceGadgetData.Filters {
				InvoiceId = invoiceId,
			});
			return (await GetInvoices(invoiceResult.Invoices)).SingleOrDefault();
		}

		public async Task<List<InvoiceFull.TaskAssignmentAndTasks>> GetTaskAssignmentAndTasks(long invoiceId)
		{
			var result = new List<InvoiceFull.TaskAssignmentAndTasks>();
			var assignments = await api.GetTaskAssignments(invoiceId);
			foreach (var assignment in assignments)
			{
				var ta = new InvoiceFull.TaskAssignmentAndTasks { TaskAssignment = assignment };
				result.Add(ta);
				ta.Task = await api.GetAllTaskInfo(assignment.TaskId, downloadImages: false);
			}
			return result;
		}

		//public async Task SomeExperiments()
		//{
		//	var lotsOfInfo = await api.GetAllTaskInfo(10551608); //assignmentId: 5018298 //70128669515

		//	var invoices = await api.GetSupplierInvoiceGadgetData(new DateTime(2019, 5, 1), DateTime.Now.Date);
		//	var found = invoices.Invoices.FirstOrDefault(i => i.Supplier.Name.StartsWith("SBC Sv Bostadsrättscentrum"));
		//	var invoice = found ?? invoices.Invoices.FirstOrDefault();
		//	if (invoice != null)
		//	{
		//		var assignments = await api.GetTaskAssignments(invoice.Id);
		//		var assignment = assignments.FirstOrDefault();
		//		if (assignment != null)
		//		{
		//			var info = api.GetAllTaskInfo(assignment.TaskId);
		//		}
		//	}
		//}
	}
}
