namespace MediusFlowAPI.Models.SupplierInvoiceGadgetData
{
	using System;
	using System.Globalization;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;

	public partial class Response
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Invoices")]
		public Invoice[] Invoices { get; set; }

		[JsonProperty("RowCount")]
		public long RowCount { get; set; }

		[JsonProperty("IsOverLimit")]
		public bool IsOverLimit { get; set; }
	}

	public partial class Invoice
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("CompanyName")]
		public CompanyName CompanyName { get; set; }

		[JsonProperty("Supplier")]
		public Supplier Supplier { get; set; }

		[JsonProperty("InvoiceNumber")]
		public string InvoiceNumber { get; set; }

		[JsonProperty("OrderNumber")]
		public string OrderNumber { get; set; }

		[JsonProperty("DeliveryNote")]
		public string DeliveryNote { get; set; }

		[JsonProperty("CustomFields")]
		public object CustomFields { get; set; }

		[JsonProperty("InvoiceDate")]
		public string InvoiceDate { get; set; }

		[JsonProperty("DueDate")]
		public string DueDate { get; set; }

		[JsonProperty("PreliminaryBookingDate")]
		public string PreliminaryBookingDate { get; set; }

		[JsonProperty("FinalBookingDate")]
		public string FinalBookingDate { get; set; }

		[JsonProperty("ActualPaymentDate")]
		public string ActualPaymentDate { get; set; }

		[JsonProperty("PreliminaryVoucherNumber")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long? PreliminaryVoucherNumber { get; set; }

		[JsonProperty("FinalVoucherNumber")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long? FinalVoucherNumber { get; set; }

		[JsonProperty("GrossAmount")]
		public GrossAmount GrossAmount { get; set; }

		[JsonProperty("NetAmount")]
		public GrossAmount NetAmount { get; set; }

		[JsonProperty("TaxAmount")]
		public GrossAmount TaxAmount { get; set; }

		[JsonProperty("CurrencyCode")]
		public string CurrencyCode { get; set; } //CurrencyCode

	[JsonProperty("CurrencyRate")]
		public long CurrencyRate { get; set; }

		[JsonProperty("NetAmountInAccountingCurrency")]
		public GrossAmount NetAmountInAccountingCurrency { get; set; }

		[JsonProperty("GrossAmountInAccountingCurrency")]
		public GrossAmount GrossAmountInAccountingCurrency { get; set; }

		[JsonProperty("TaxAmountInAccountingCurrency")]
		public GrossAmount TaxAmountInAccountingCurrency { get; set; }

		[JsonProperty("InvoiceTypeName")]
		public InvoiceTypeName InvoiceTypeName { get; set; }

		[JsonProperty("CurrentTask")]
		public string CurrentTask { get; set; } //CurrentTask

		[JsonProperty("CurrentHandlers")]
		public string CurrentHandlers { get; set; }

		[JsonProperty("ContractNumber")]
		public string ContractNumber { get; set; }

		[JsonProperty("LabelsString")]
		public string LabelsString { get; set; }

		[JsonProperty("AssignedLabelsIds")]
		public object[] AssignedLabelsIds { get; set; }
	}

	public partial class GrossAmount
	{
		[JsonProperty("$type")]
		public TypeEnum Type { get; set; }

		[JsonProperty("CurrencyCode")]
		public string CurrencyCode { get; set; } //CurrencyCode

		[JsonProperty("DisplayValue")]
		public string DisplayValue { get; set; }
	}

	public partial class Supplier
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("SupplierId")]
		public string SupplierId { get; set; }

		[JsonProperty("Identifiers")]
		public Identifier[] Identifiers { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("Type")]
		public string SupplierType { get; set; }

		[JsonProperty("DisplayName")]
		public string DisplayName { get; set; }

		[JsonProperty("HasAccess")]
		public bool HasAccess { get; set; }
	}

	public partial class Identifier
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Value")]
		public string Value { get; set; }
	}

	public enum CompanyName { Riksrådsvägen6297 };

	//public enum CurrencyCode { Sek };

	//public enum CurrentHandlers { Empty, Slutattestant };

	//public enum CurrentTask { Arkiverad, Attestera, Makulerad };

	public enum TypeEnum { MediusCoreDtOsAmountDtoMediusCoreCommon };

	public enum InvoiceTypeName { MediusExpenseInvoiceEntitiesExpenseInvoice };

	public partial class Response
	{
		public static Response FromJson(string json) => JsonConvert.DeserializeObject<Response>(json, Models.SupplierInvoiceGadgetData.ResponseConverter.Settings);
	}

	public static class ResponseSerialize
	{
		public static string ToJson(this Response self) => JsonConvert.SerializeObject(self, Models.SupplierInvoiceGadgetData.ResponseConverter.Settings);
	}

	internal static class ResponseConverter
	{
		public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
			DateParseHandling = DateParseHandling.None,
			Converters =
			{
				CompanyNameConverter.Singleton,
				//CurrencyCodeConverter.Singleton,
				//CurrentHandlersConverter.Singleton,
				//CurrentTaskConverter.Singleton,
				TypeEnumConverter.Singleton,
				InvoiceTypeNameConverter.Singleton,
				new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
			},
		};
	}

	internal class CompanyNameConverter : JsonConverter
	{
		public override bool CanConvert(Type t) => t == typeof(CompanyName) || t == typeof(CompanyName?);

		public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var value = serializer.Deserialize<string>(reader);
			if (value == "Riksrådsvägen(6297)")
			{
				return CompanyName.Riksrådsvägen6297;
			}
			throw new Exception("Cannot unmarshal type CompanyName");
		}

		public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
		{
			if (untypedValue == null)
			{
				serializer.Serialize(writer, null);
				return;
			}
			var value = (CompanyName)untypedValue;
			if (value == CompanyName.Riksrådsvägen6297)
			{
				serializer.Serialize(writer, "Riksrådsvägen(6297)");
				return;
			}
			throw new Exception("Cannot marshal type CompanyName");
		}

		public static readonly CompanyNameConverter Singleton = new CompanyNameConverter();
	}

	//internal class CurrencyCodeConverter : JsonConverter
	//{
	//	public override bool CanConvert(Type t) => t == typeof(CurrencyCode) || t == typeof(CurrencyCode?);

	//	public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
	//	{
	//		if (reader.TokenType == JsonToken.Null) return null;
	//		var value = serializer.Deserialize<string>(reader);
	//		if (value == "SEK")
	//		{
	//			return CurrencyCode.Sek;
	//		}
	//		throw new Exception("Cannot unmarshal type CurrencyCode");
	//	}

	//	public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
	//	{
	//		if (untypedValue == null)
	//		{
	//			serializer.Serialize(writer, null);
	//			return;
	//		}
	//		var value = (CurrencyCode)untypedValue;
	//		if (value == CurrencyCode.Sek)
	//		{
	//			serializer.Serialize(writer, "SEK");
	//			return;
	//		}
	//		throw new Exception("Cannot marshal type CurrencyCode");
	//	}

	//	public static readonly CurrencyCodeConverter Singleton = new CurrencyCodeConverter();
	//}

	//internal class CurrentHandlersConverter : JsonConverter
	//{
	//	public override bool CanConvert(Type t) => t == typeof(CurrentHandlers) || t == typeof(CurrentHandlers?);

	//	public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
	//	{
	//		if (reader.TokenType == JsonToken.Null) return null;
	//		var value = serializer.Deserialize<string>(reader);
	//		switch (value)
	//		{
	//			case "":
	//				return CurrentHandlers.Empty;
	//			case "Slutattestant":
	//				return CurrentHandlers.Slutattestant;
	//		}
	//		throw new Exception("Cannot unmarshal type CurrentHandlers");
	//	}

	//	public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
	//	{
	//		if (untypedValue == null)
	//		{
	//			serializer.Serialize(writer, null);
	//			return;
	//		}
	//		var value = (CurrentHandlers)untypedValue;
	//		switch (value)
	//		{
	//			case CurrentHandlers.Empty:
	//				serializer.Serialize(writer, "");
	//				return;
	//			case CurrentHandlers.Slutattestant:
	//				serializer.Serialize(writer, "Slutattestant");
	//				return;
	//		}
	//		throw new Exception("Cannot marshal type CurrentHandlers");
	//	}

	//	public static readonly CurrentHandlersConverter Singleton = new CurrentHandlersConverter();
	//}

	//internal class CurrentTaskConverter : JsonConverter
	//{
	//	public override bool CanConvert(Type t) => t == typeof(CurrentTask) || t == typeof(CurrentTask?);

	//	public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
	//	{
	//		if (reader.TokenType == JsonToken.Null) return null;
	//		var value = serializer.Deserialize<string>(reader);
	//		switch (value)
	//		{
	//			case "Arkiverad":
	//				return CurrentTask.Arkiverad;
	//			case "Attestera":
	//				return CurrentTask.Attestera;
	//			case "Makulerad":
	//				return CurrentTask.Makulerad;
	//		}
	//		throw new Exception($"Cannot unmarshal type CurrentTask {value}");
	//	}

	//	public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
	//	{
	//		if (untypedValue == null)
	//		{
	//			serializer.Serialize(writer, null);
	//			return;
	//		}
	//		var value = (CurrentTask)untypedValue;
	//		switch (value)
	//		{
	//			case CurrentTask.Arkiverad:
	//				serializer.Serialize(writer, "Arkiverad");
	//				return;
	//			case CurrentTask.Attestera:
	//				serializer.Serialize(writer, "Attestera");
	//				return;
	//		}
	//		throw new Exception("Cannot marshal type CurrentTask");
	//	}

	//	public static readonly CurrentTaskConverter Singleton = new CurrentTaskConverter();
	//}

	internal class ParseStringConverter : JsonConverter
	{
		public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

		public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var value = serializer.Deserialize<string>(reader);
			long l;
			if (Int64.TryParse(value, out l))
			{
				return l;
			}
			if (Nullable.GetUnderlyingType(t) != null)
				return null;
			throw new Exception("Cannot unmarshal type long");
		}

		public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
		{
			if (untypedValue == null)
			{
				serializer.Serialize(writer, null);
				return;
			}
			var value = (long)untypedValue;
			serializer.Serialize(writer, value.ToString());
			return;
		}

		public static readonly ParseStringConverter Singleton = new ParseStringConverter();
	}

	internal class TypeEnumConverter : JsonConverter
	{
		public override bool CanConvert(Type t) => t == typeof(TypeEnum) || t == typeof(TypeEnum?);

		public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var value = serializer.Deserialize<string>(reader);
			if (value == "Medius.Core.DTOs.AmountDto, Medius.Core.Common")
			{
				return TypeEnum.MediusCoreDtOsAmountDtoMediusCoreCommon;
			}
			throw new Exception("Cannot unmarshal type TypeEnum");
		}

		public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
		{
			if (untypedValue == null)
			{
				serializer.Serialize(writer, null);
				return;
			}
			var value = (TypeEnum)untypedValue;
			if (value == TypeEnum.MediusCoreDtOsAmountDtoMediusCoreCommon)
			{
				serializer.Serialize(writer, "Medius.Core.DTOs.AmountDto, Medius.Core.Common");
				return;
			}
			throw new Exception("Cannot marshal type TypeEnum");
		}

		public static readonly TypeEnumConverter Singleton = new TypeEnumConverter();
	}

	internal class InvoiceTypeNameConverter : JsonConverter
	{
		public override bool CanConvert(Type t) => t == typeof(InvoiceTypeName) || t == typeof(InvoiceTypeName?);

		public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var value = serializer.Deserialize<string>(reader);
			if (value == "Medius.ExpenseInvoice.Entities.ExpenseInvoice")
			{
				return InvoiceTypeName.MediusExpenseInvoiceEntitiesExpenseInvoice;
			}
			throw new Exception("Cannot unmarshal type InvoiceTypeName");
		}

		public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
		{
			if (untypedValue == null)
			{
				serializer.Serialize(writer, null);
				return;
			}
			var value = (InvoiceTypeName)untypedValue;
			if (value == InvoiceTypeName.MediusExpenseInvoiceEntitiesExpenseInvoice)
			{
				serializer.Serialize(writer, "Medius.ExpenseInvoice.Entities.ExpenseInvoice");
				return;
			}
			throw new Exception("Cannot marshal type InvoiceTypeName");
		}

		public static readonly InvoiceTypeNameConverter Singleton = new InvoiceTypeNameConverter();
	}
}
