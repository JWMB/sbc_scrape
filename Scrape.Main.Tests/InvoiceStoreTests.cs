using MediusFlowAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SBCScan;
using Scrape.IO.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Scrape.Main.Tests
{
	[TestClass]
	public class InvoiceStoreTests
	{
		[TestMethod]
		public async Task MyTestMethod()
		{
			var underlyingStore = new InMemoryKVStore();
			var store = new InvoiceStore(underlyingStore);

			var invoice = new InvoiceFull
			{
				Invoice = new MediusFlowAPI.Models.SupplierInvoiceGadgetData.Invoice {
					InvoiceDate = DateTime.Today.ToMediusDate(),
					Supplier = new MediusFlowAPI.Models.SupplierInvoiceGadgetData.Supplier { Name = "Supplier" },
					Id = 1
				},
				TaskAssignments = new List<InvoiceFull.TaskAssignmentAndTasks> {
					new InvoiceFull.TaskAssignmentAndTasks {
						Task = new InvoiceFull.TaskFull { Task = new MediusFlowAPI.Models.Task.Response { CreatedTimestamp = DateTime.Today.ToMediusDate(), State = 1  } }
					}
				}
				
			};

			await store.Post(invoice);

			var filenameFormat = InvoiceFull.FilenameFormat.Parse(InvoiceFull.FilenameFormat.Create(invoice));
			var retrieved = await underlyingStore.Get(filenameFormat.ToString());
			if (retrieved is string serialized)
				Newtonsoft.Json.JsonConvert.DeserializeObject(serialized);
			else
				throw new FormatException("Not a string");

			//var xx = await store.Get(filenameFormat);
		}
	}
}
