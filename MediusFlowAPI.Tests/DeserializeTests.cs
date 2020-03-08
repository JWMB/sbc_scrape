using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MediusFlowAPI.Tests
{
	[TestClass]
	public class DeserializeTests
	{
		[TestInitialize]
		public void Initialize()
		{

		}

		[TestMethod]
		public void DeserializeInvoice()
		{
			var jObj = TestUtils.LoadJsonFromFile<JObject>(GetScrapedFolder(), "2019-08-06_SUEZ Recycling _5394744_08-26_2.json");
			var errors = new List<string>();
			var invoice = JsonConvert.DeserializeObject<InvoiceFull>(jObj.ToString(), new JsonSerializerSettings
			{
				Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
				{
					errors.Add($"{args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
					args.ErrorContext.Handled = true;
				},
			});
			errors.ShouldBeEmpty();
		}

		[TestMethod]
		public void DeserializeAllInvoices()
		{
			var files = Directory.GetFiles(GetScrapedFolder(), "*.json");
			var deserialized = files.ToList().Select(file => {
				var jObj = TestUtils.LoadJsonFromFile<JObject>(GetScrapedFolder(), file);
				var errors = new List<string>();
				var invoice = JsonConvert.DeserializeObject<InvoiceFull>(jObj.ToString(), new JsonSerializerSettings
				{
					Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
					{
						errors.Add($"{args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
						args.ErrorContext.Handled = true;
					},
				});
				return new { Invoice = invoice, Errors = errors };
			}).ToList();
			var withErrors = deserialized.Where(o => o.Errors.Any());
			withErrors.ShouldBeEmpty();
		}

		private string GetScrapedFolder()
		{
			var n = this.GetType().Assembly.GetName().Name;
			return TestUtils.GetFolder("sbc_scrape/scraped", n);
		}
	}
}
