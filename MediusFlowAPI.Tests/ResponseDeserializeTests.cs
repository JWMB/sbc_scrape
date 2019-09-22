using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.IO;
using System.Linq;

namespace MediusFlowAPI.Tests
{
	[TestClass]
	public class ResponseDeserializeTests
	{
		[TestInitialize]
		public void Init()
		{
			AnonymizeFilesInFolder("*.json");
		}
		private string GetTestDataFolder()
		{
			var folder = Environment.CurrentDirectory;
			var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
			if (folder.Contains(bin))
				folder = folder.Remove(folder.LastIndexOf(bin));
			return Path.Combine(folder, "TestData");
		}

		private T LoadJsonFromFile<T>(string path) where T : JToken 
		{
			path = Path.Combine(GetTestDataFolder(), path);
			if (!File.Exists(path))
				throw new FileNotFoundException(path);
			var json = File.ReadAllText(path);
			if (json.Length == 0)
				throw new FileLoadException($"File empty: {path}");
			var token = JToken.Parse(json);
			if (token is T)
				return token as T;
			throw new Exception($"JSON in '{path}' is not a {typeof(T).Name}");
		}

		private JToken GetAnonymized(string path)
		{
			var jToken = LoadJsonFromFile<JToken>(path);

			var valuesToReplace = new[] {
				"$..Supplier.Name", "$..AuthorizerName", "$..OnBehalfOfUserName", "$..Company.Name",
				"$..OrganizationNumber", "$..author", "$..CompanyId", "$..CompanyName" };
			valuesToReplace.ToList().ForEach(selector => {
				var found = jToken.SelectTokens(selector).Where(o => o is JValue).Cast<JValue>().ToList();
				found.ForEach(o => {
					//Replace with anonymous value
					o.Value = o.Type == JTokenType.String
						? (object)$"Anon"
						: (object)12345;
				});
			});
			//ExternalSystemId

			return jToken;
		}

		private void AnonymizeFilesInFolder(string filePattern)
		{
			var files = new DirectoryInfo(GetTestDataFolder()).GetFiles(filePattern).ToList();
			files.ForEach(file => {
				var anon = GetAnonymized(file.Name);
				File.WriteAllText(file.FullName, anon.ToString());
			});
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
			//Models.Comment2.Response[] response = null;
			var response = Models.Comment2.Response.FromJson(jArr.ToString());
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
