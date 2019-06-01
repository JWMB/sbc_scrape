namespace MediusFlowAPI.Models.SupplierInvoiceGadgetData
{
	using System;
	using System.Collections.Generic;

	using System.Globalization;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;

	public partial class Request
	{
		[JsonProperty("filters")]
		public Filters Filters { get; set; }

		[JsonProperty("actualPage")]
		public long ActualPage { get; set; } = 1L;

		[JsonProperty("pageSize")]
		public long PageSize { get; set; } = 10L;
	}

	public partial class Filters
	{
		[JsonProperty("CompanyHierarchyId")]
		public string CompanyHierarchyId { get; set; }

		[JsonProperty("SupplierId")]
		public long SupplierId { get; set; }

		[JsonProperty("InvoiceId")]
		public object InvoiceId { get; set; }

		[JsonProperty("InvoiceNumber")]
		public object InvoiceNumber { get; set; }

		[JsonProperty("OrderNumber")]
		public object OrderNumber { get; set; }

		[JsonProperty("SearchedLabels")]
		public object[] SearchedLabels { get; set; } = new object[] { };

		[JsonProperty("TextCustomField1")]
		public object TextCustomField1 { get; set; }

		[JsonProperty("TextCustomField2")]
		public object TextCustomField2 { get; set; }

		[JsonProperty("TextCustomField3")]
		public object TextCustomField3 { get; set; }

		[JsonProperty("TextCustomField4")]
		public object TextCustomField4 { get; set; }

		[JsonProperty("TextCustomField5")]
		public object TextCustomField5 { get; set; }

		[JsonProperty("BooleanCustomField1")]
		public object BooleanCustomField1 { get; set; }

		[JsonProperty("BooleanCustomField2")]
		public object BooleanCustomField2 { get; set; }

		[JsonProperty("BooleanCustomField3")]
		public object BooleanCustomField3 { get; set; }

		[JsonProperty("BooleanCustomField4")]
		public object BooleanCustomField4 { get; set; }

		[JsonProperty("BooleanCustomField5")]
		public object BooleanCustomField5 { get; set; }

		[JsonProperty("NumericCustomField1From")]
		public object NumericCustomField1From { get; set; }

		[JsonProperty("NumericCustomField1To")]
		public object NumericCustomField1To { get; set; }

		[JsonProperty("NumericCustomField2From")]
		public object NumericCustomField2From { get; set; }

		[JsonProperty("NumericCustomField2To")]
		public object NumericCustomField2To { get; set; }

		[JsonProperty("NumericCustomField3From")]
		public object NumericCustomField3From { get; set; }

		[JsonProperty("NumericCustomField3To")]
		public object NumericCustomField3To { get; set; }

		[JsonProperty("NumericCustomField4From")]
		public object NumericCustomField4From { get; set; }

		[JsonProperty("NumericCustomField4To")]
		public object NumericCustomField4To { get; set; }

		[JsonProperty("NumericCustomField5From")]
		public object NumericCustomField5From { get; set; }

		[JsonProperty("NumericCustomField5To")]
		public object NumericCustomField5To { get; set; }

		[JsonProperty("PreliminaryVoucherNumber")]
		public object PreliminaryVoucherNumber { get; set; }

		[JsonProperty("PreliminaryBookingDateFrom")]
		public object PreliminaryBookingDateFrom { get; set; }

		[JsonProperty("PreliminaryBookingDateTo")]
		public object PreliminaryBookingDateTo { get; set; }

		[JsonProperty("FinalVoucherNumber")]
		public object FinalVoucherNumber { get; set; }

		[JsonProperty("FinalBookingDateFrom")]
		public object FinalBookingDateFrom { get; set; }

		[JsonProperty("FinalBookingDateTo")]
		public object FinalBookingDateTo { get; set; }

		[JsonProperty("InvoiceDateFrom")]
		public object InvoiceDateFrom { get; set; }

		[JsonProperty("InvoiceDateTo")]
		public object InvoiceDateTo { get; set; }

		[JsonProperty("DueDateFrom")]
		public object DueDateFrom { get; set; }

		[JsonProperty("DueDateTo")]
		public object DueDateTo { get; set; }

		[JsonProperty("GrossAmountFrom")]
		public object GrossAmountFrom { get; set; }

		[JsonProperty("GrossAmountTo")]
		public object GrossAmountTo { get; set; }

		[JsonProperty("InvoiceTypeName")]
		public object InvoiceTypeName { get; set; }

		[JsonProperty("InvoiceStatus")]
		public string InvoiceStatus { get; set; } = "all";

		[JsonProperty("Dimension1")]
		public string Dimension1 { get; set; } = "";

		[JsonProperty("Dimension2")]
		public string Dimension2 { get; set; } = "";

		[JsonProperty("Dimension3")]
		public string Dimension3 { get; set; } = "";

		[JsonProperty("Dimension4")]
		public string Dimension4 { get; set; } = "";

		[JsonProperty("Dimension5")]
		public string Dimension5 { get; set; } = "";

		[JsonProperty("Dimension6")]
		public string Dimension6 { get; set; } = "";

		[JsonProperty("Dimension7")]
		public string Dimension7 { get; set; } = "";

		[JsonProperty("Dimension8")]
		public string Dimension8 { get; set; } = "";

		[JsonProperty("Dimension9")]
		public string Dimension9 { get; set; } = "";

		[JsonProperty("Dimension10")]
		public string Dimension10 { get; set; } = "";

		[JsonProperty("Dimension11")]
		public string Dimension11 { get; set; } = "";

		[JsonProperty("Dimension12")]
		public string Dimension12 { get; set; } = "";

		[JsonProperty("FreeTextDimension1")]
		public string FreeTextDimension1 { get; set; } = "";

		[JsonProperty("FreeTextDimension2")]
		public string FreeTextDimension2 { get; set; } = "";

		[JsonProperty("FreeTextDimension3")]
		public string FreeTextDimension3 { get; set; } = "";

		[JsonProperty("FreeTextDimension4")]
		public string FreeTextDimension4 { get; set; } = "";

		[JsonProperty("FreeTextDimension5")]
		public string FreeTextDimension5 { get; set; } = "";

		[JsonProperty("DeliveryNote")]
		public object DeliveryNote { get; set; }

		[JsonProperty("ActualPaymentDateFrom")]
		public object ActualPaymentDateFrom { get; set; }

		[JsonProperty("ActualPaymentDateTo")]
		public object ActualPaymentDateTo { get; set; }

		[JsonProperty("ContractNumber")]
		public object ContractNumber { get; set; }
	}

	public partial class Request
	{
		public static Request FromJson(string json) => JsonConvert.DeserializeObject<Request>(json, Models.SupplierInvoiceGadgetData.Converter.Settings);
	}

	public static class Serialize
	{
		public static string ToJson(this Request self) => JsonConvert.SerializeObject(self, Models.SupplierInvoiceGadgetData.Converter.Settings);
	}

	internal static class Converter
	{
		public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
			DateParseHandling = DateParseHandling.None,
			Converters =
			{
				new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
			},
		};
	}
}
