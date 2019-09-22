using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediusFlowAPI.Tests
{
	[TestClass]
	public class DeserializeTests
	{
		[TestMethod]
		public void DeserializeInvoice()
		{
			var n = this.GetType().Assembly.GetName().Name;
			var folder = TestUtils.GetFolder("sbc_scrape/scraped", n);
			var jObj = TestUtils.LoadJsonFromFile<JObject>(folder, "2019-08-06_SUEZ Recycling _5394744_08-26_2.json");
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

	}
}
