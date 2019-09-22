using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.Linq;

namespace MediusFlowAPI.Tests
{
	[TestClass]
	public class ResponseDeserializeTests
	{
		[TestInitialize]
		public void Init()
		{
			//AnonymizeFilesInFolder("TestData", "*.json");
		}

		private T LoadJsonFromFile<T>(string file) where T : JToken
		{
			return TestUtils.LoadJsonFromFile<T>("TestData", file);
		}

		[TestMethod]
		public void SupplierInvoiceGadgetData()
		{
			var jObj = LoadJsonFromFile<JObject>(@"GetSupplierInvoiceGadgetData.json");
			Models.SupplierInvoiceGadgetData.Response response = null;
			Should.NotThrow(() => response = Models.SupplierInvoiceGadgetData.Response.FromJson(jObj.ToString()));
		}

		[TestMethod]
		public void GetAccountingObject()
		{
			var jObj = LoadJsonFromFile<JObject>(@"GetAccountingObjectWithLinesForInvoice.json");
			Models.AccountingObjectWithLinesForInvoice.Response response = null;
			Should.NotThrow(() => response = Models.AccountingObjectWithLinesForInvoice.Response.FromJson(jObj.ToString()));
		}

		[TestMethod]
		public void GetTask()
		{
			var jObj = LoadJsonFromFile<JObject>(@"GetTask.json");
			Models.Task.Response response = null;
			Should.NotThrow(() => response = Models.Task.Response.FromJson(jObj.ToString()));
		}

		[TestMethod]
		public void GetComments()
		{
			var jArr = LoadJsonFromFile<JArray>(@"comments.json");
			Models.Comment2.Response.FromJson(jArr.ToString());
		}
	}
	/*
https://webbattestxi.sbc.se/Mediusflow/Backend/Rpc/TabManager/GetTaskTabsForId
{"taskId":11469852}

https://webbattestxi.sbc.se/Mediusflow/Backend/Rest/HashFilesService/GetHashFilesWithNames
{"entityId":5497730,"entityType":"Medius.ExpenseInvoice.Entities.ExpenseInvoice"}

https://webbattestxi.sbc.se/Mediusflow/Backend/Rpc/ExpenseInvoiceService/GetLines
{"invoiceId":5497730}

https://webbattestxi.sbc.se/Mediusflow/Backend/Rpc/TemplateAccountingObjectService/GetMostRecentLatestAccountingTemplate
{"companyId":2404,"supplierId":29733,"currencyId":2,"allUsers":false}

https://webbattestxi.sbc.se/Mediusflow/Backend/Rest/comments/?entityViewId=1b48d5d8-6788-456a-9397-ab9643a5640e&entityType=Medius.ExpenseInvoice.Entities.ExpenseInvoice&_=1569073939309
	 */
}
