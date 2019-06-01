﻿// <auto-generated />
//
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using QuickType;
//
//    var response = Response.FromJson(jsonString);

namespace MediusFlowAPI.Models.Task
{
	using System;
	using System.Collections.Generic;

	using System.Globalization;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;

	public partial class Response
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Tag")]
		public string Tag { get; set; }

		[JsonProperty("Document")]
		public Document Document { get; set; }

		[JsonProperty("Perspective")]
		public ResponsePerspective Perspective { get; set; }

		[JsonProperty("Created")]
		public string Created { get; set; }

		[JsonProperty("State")]
		public long State { get; set; }

		[JsonProperty("Description")]
		public string Description { get; set; }

		[JsonProperty("WorkflowInstance")]
		public Guid WorkflowInstance { get; set; }

		[JsonProperty("WorkflowContext")]
		public WorkflowContext WorkflowContext { get; set; }

		[JsonProperty("ActivityContext")]
		public ActivityContext ActivityContext { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public HashFile[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class ActivityContext
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("IsUserActivity")]
		public bool IsUserActivity { get; set; }

		[JsonProperty("Workflow")]
		public object Workflow { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Document
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("ContractNumber")]
		public string ContractNumber { get; set; }

		[JsonProperty("DeliveryNote")]
		public string DeliveryNote { get; set; }

		[JsonProperty("OrderIdentifier")]
		public string OrderIdentifier { get; set; }

		[JsonProperty("OrderIdentifier2")]
		public object OrderIdentifier2 { get; set; }

		[JsonProperty("PaymentReference")]
		public string PaymentReference { get; set; }

		[JsonProperty("ActualPaymentDate")]
		public object ActualPaymentDate { get; set; }

		[JsonProperty("PreferredPaymentDate")]
		public string PreferredPaymentDate { get; set; }

		[JsonProperty("Supplier")]
		public Supplier Supplier { get; set; }

		[JsonProperty("PaymentDetails")]
		public object PaymentDetails { get; set; }

		[JsonProperty("InvoicingSupplier")]
		public object InvoicingSupplier { get; set; }

		[JsonProperty("Lines")]
		public object[] Lines { get; set; }

		[JsonProperty("GetInvoiceType")]
		public string GetInvoiceType { get; set; }

		[JsonProperty("Perspective")]
		public DocumentPerspective Perspective { get; set; }

		[JsonProperty("IsPaymentBlocked")]
		public bool IsPaymentBlocked { get; set; }

		[JsonProperty("Company")]
		public Company Company { get; set; }

		[JsonProperty("InvoiceNumber")]
		public string InvoiceNumber { get; set; }

		[JsonProperty("InvoiceDate")]
		public string InvoiceDate { get; set; }

		[JsonProperty("DueDate")]
		public string DueDate { get; set; }

		[JsonProperty("InvoiceReference")]
		public string InvoiceReference { get; set; }

		[JsonProperty("PreliminaryBookingDate")]
		public string PreliminaryBookingDate { get; set; }

		[JsonProperty("FinalBookingDate")]
		public string FinalBookingDate { get; set; }

		[JsonProperty("CustomFields")]
		public CustomFields CustomFields { get; set; }

		[JsonProperty("Accounting")]
		public Accounting Accounting { get; set; }

		[JsonProperty("Gross")]
		public Amount Gross { get; set; }

		[JsonProperty("Amount")]
		public Amount Amount { get; set; }

		[JsonProperty("VoucherObject")]
		public VoucherObject VoucherObject { get; set; }

		[JsonProperty("Tax")]
		public Amount Tax { get; set; }

		[JsonProperty("Rounding")]
		public Amount Rounding { get; set; }

		[JsonProperty("TransactionRate")]
		public TransactionRate TransactionRate { get; set; }

		[JsonProperty("CurrencyRate")]
		public long CurrencyRate { get; set; }

		[JsonProperty("IsCurrencyRateImported")]
		public bool IsCurrencyRateImported { get; set; }

		[JsonProperty("TaxIndicator1")]
		public object TaxIndicator1 { get; set; }

		[JsonProperty("TaxIndicator2")]
		public object TaxIndicator2 { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public HashFile[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Accounting
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("BalanceCodeString")]
		public object BalanceCodeString { get; set; }

		[JsonProperty("TaxGroup")]
		public object TaxGroup { get; set; }

		[JsonProperty("TaxIndicator1")]
		public object TaxIndicator1 { get; set; }

		[JsonProperty("TaxIndicator2")]
		public object TaxIndicator2 { get; set; }

		[JsonProperty("TaxCoding")]
		public object[] TaxCoding { get; set; }

		[JsonProperty("UseTaxCodes")]
		public bool UseTaxCodes { get; set; }

		[JsonProperty("UseTaxCodesOnHead")]
		public bool UseTaxCodesOnHead { get; set; }

		[JsonProperty("UseTwoTaxIndicators")]
		public bool UseTwoTaxIndicators { get; set; }

		[JsonProperty("Company")]
		public Company Company { get; set; }

		[JsonProperty("DocumentType")]
		public string DocumentType { get; set; }

		[JsonProperty("TransactionCurrency")]
		public Currency TransactionCurrency { get; set; }

		[JsonProperty("TransactionDate")]
		public string TransactionDate { get; set; }

		[JsonProperty("DimensionStrings")]
		public object[] DimensionStrings { get; set; }

		[JsonProperty("TotalNet")]
		public Amount TotalNet { get; set; }

		[JsonProperty("TotalTax")]
		public Amount TotalTax { get; set; }

		[JsonProperty("TotalRounding")]
		public Amount TotalRounding { get; set; }

		[JsonProperty("TaxTolerance")]
		public object TaxTolerance { get; set; }

		[JsonProperty("Perspective")]
		public object Perspective { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public HashFile[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Company
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("OrganizationNumber")]
		public string OrganizationNumber { get; set; }

		[JsonProperty("ParentCompany")]
		public object ParentCompany { get; set; }

		[JsonProperty("Parent")]
		public object Parent { get; set; }

		[JsonProperty("IsVirtual")]
		public bool IsVirtual { get; set; }

		[JsonProperty("AccountingCurrency")]
		public Currency AccountingCurrency { get; set; }

		[JsonProperty("CompanyId")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long CompanyId { get; set; }

		[JsonProperty("HierarchyId")]
		public string HierarchyId { get; set; }

		[JsonProperty("Language")]
		public object Language { get; set; }

		[JsonProperty("Addresses")]
		public object[] Addresses { get; set; }

		[JsonProperty("CompanyIdentifiers")]
		public object[] CompanyIdentifiers { get; set; }

		[JsonProperty("IsActive")]
		public bool IsActive { get; set; }

		[JsonProperty("ExternalSystemId")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long ExternalSystemId { get; set; }

		[JsonProperty("CustomFields")]
		public CustomFields CustomFields { get; set; }

		[JsonProperty("ImportedTimestamp")]
		public string ImportedTimestamp { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Currency
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Code")]
		public string Code { get; set; }

		[JsonProperty("IsActive")]
		public bool IsActive { get; set; }

		[JsonProperty("ExternalSystemId")]
		public string ExternalSystemId { get; set; }

		[JsonProperty("CustomFields")]
		public CustomFields CustomFields { get; set; }

		[JsonProperty("ImportedTimestamp")]
		public string ImportedTimestamp { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class CustomFields
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Perspective")]
		public CustomFieldsPerspective Perspective { get; set; }

		[JsonProperty("TextCustomField1")]
		public string TextCustomField1 { get; set; }

		[JsonProperty("TextCustomField2")]
		public string TextCustomField2 { get; set; }

		[JsonProperty("TextCustomField3")]
		public string TextCustomField3 { get; set; }

		[JsonProperty("TextCustomField4")]
		public string TextCustomField4 { get; set; }

		[JsonProperty("TextCustomField5")]
		public string TextCustomField5 { get; set; }

		[JsonProperty("NumericCustomField1")]
		public double? NumericCustomField1 { get; set; }

		[JsonProperty("NumericCustomField2")]
		public long? NumericCustomField2 { get; set; }

		[JsonProperty("NumericCustomField3")]
		public long? NumericCustomField3 { get; set; }

		[JsonProperty("NumericCustomField4")]
		public long? NumericCustomField4 { get; set; }

		[JsonProperty("NumericCustomField5")]
		public long? NumericCustomField5 { get; set; }

		[JsonProperty("BooleanCustomField1")]
		public bool? BooleanCustomField1 { get; set; }

		[JsonProperty("BooleanCustomField2")]
		public bool? BooleanCustomField2 { get; set; }

		[JsonProperty("BooleanCustomField3")]
		public bool? BooleanCustomField3 { get; set; }

		[JsonProperty("BooleanCustomField4")]
		public bool? BooleanCustomField4 { get; set; }

		[JsonProperty("BooleanCustomField5")]
		public bool? BooleanCustomField5 { get; set; }

		[JsonProperty("DateTimeCustomField1")]
		public object DateTimeCustomField1 { get; set; }

		[JsonProperty("DateTimeCustomField2")]
		public object DateTimeCustomField2 { get; set; }

		[JsonProperty("DateTimeCustomField3")]
		public object DateTimeCustomField3 { get; set; }

		[JsonProperty("DateTimeCustomField4")]
		public object DateTimeCustomField4 { get; set; }

		[JsonProperty("DateTimeCustomField5")]
		public object DateTimeCustomField5 { get; set; }

		[JsonProperty("InternalIdentifier")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long InternalIdentifier { get; set; }
	}

	public partial class CustomFieldsPerspective
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Configuration")]
		public Configuration Configuration { get; set; }

		[JsonProperty("AreAnyFieldsActive")]
		public bool AreAnyFieldsActive { get; set; }

		[JsonProperty("ValidationErrors")]
		public object ValidationErrors { get; set; }
	}

	public partial class Configuration
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("EntityType")]
		public object EntityType { get; set; }

		[JsonProperty("TextCustomField1")]
		public ArakGroundhog TextCustomField1 { get; set; }

		[JsonProperty("TextCustomField2")]
		public ArakGroundhog TextCustomField2 { get; set; }

		[JsonProperty("TextCustomField3")]
		public ArakGroundhog TextCustomField3 { get; set; }

		[JsonProperty("TextCustomField4")]
		public ArakGroundhog TextCustomField4 { get; set; }

		[JsonProperty("TextCustomField5")]
		public ArakGroundhog TextCustomField5 { get; set; }

		[JsonProperty("NumericCustomField1")]
		public ArakGroundhog NumericCustomField1 { get; set; }

		[JsonProperty("NumericCustomField2")]
		public ArakGroundhog NumericCustomField2 { get; set; }

		[JsonProperty("NumericCustomField3")]
		public ArakGroundhog NumericCustomField3 { get; set; }

		[JsonProperty("NumericCustomField4")]
		public ArakGroundhog NumericCustomField4 { get; set; }

		[JsonProperty("NumericCustomField5")]
		public ArakGroundhog NumericCustomField5 { get; set; }

		[JsonProperty("BooleanCustomField1")]
		public ArakGroundhog BooleanCustomField1 { get; set; }

		[JsonProperty("BooleanCustomField2")]
		public ArakGroundhog BooleanCustomField2 { get; set; }

		[JsonProperty("BooleanCustomField3")]
		public ArakGroundhog BooleanCustomField3 { get; set; }

		[JsonProperty("BooleanCustomField4")]
		public ArakGroundhog BooleanCustomField4 { get; set; }

		[JsonProperty("BooleanCustomField5")]
		public ArakGroundhog BooleanCustomField5 { get; set; }

		[JsonProperty("DateTimeCustomField1")]
		public ArakGroundhog DateTimeCustomField1 { get; set; }

		[JsonProperty("DateTimeCustomField2")]
		public ArakGroundhog DateTimeCustomField2 { get; set; }

		[JsonProperty("DateTimeCustomField3")]
		public ArakGroundhog DateTimeCustomField3 { get; set; }

		[JsonProperty("DateTimeCustomField4")]
		public ArakGroundhog DateTimeCustomField4 { get; set; }

		[JsonProperty("DateTimeCustomField5")]
		public ArakGroundhog DateTimeCustomField5 { get; set; }

		[JsonProperty("InternalIdentifier")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long InternalIdentifier { get; set; }
	}

	public partial class ArakGroundhog
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Mandatory")]
		public bool Mandatory { get; set; }

		[JsonProperty("IsActive")]
		public bool IsActive { get; set; }

		[JsonProperty("DisableIntegrationUpdates")]
		public bool DisableIntegrationUpdates { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("InternalIdentifier")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long InternalIdentifier { get; set; }

		[JsonProperty("MaximumLength")]
		public object MaximumLength { get; set; }
	}

	public partial class Amount
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("CurrencyCode")]
		public string CurrencyCode { get; set; }

		[JsonProperty("DisplayValue")]
		public string DisplayValue { get; set; }
	}

	public partial class HashFile
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Hash")]
		public Guid Hash { get; set; }

		[JsonProperty("Type")]
		public string HashFileType { get; set; }

		[JsonProperty("FileInfo")]
		public object FileInfo { get; set; }
	}

	public partial class DocumentPerspective
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("SupplierPaymentTerm")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long SupplierPaymentTerm { get; set; }

		[JsonProperty("RoundingAmount")]
		public Amount RoundingAmount { get; set; }

		[JsonProperty("UsePaymentBlock")]
		public bool UsePaymentBlock { get; set; }

		[JsonProperty("ShowPaymentDetails")]
		public bool ShowPaymentDetails { get; set; }

		[JsonProperty("CompanyConfiguration")]
		public CompanyConfiguration CompanyConfiguration { get; set; }

		[JsonProperty("ValidationErrors")]
		public object ValidationErrors { get; set; }
	}

	public partial class CompanyConfiguration
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Dimension1")]
		public Dimension1 Dimension1 { get; set; }

		[JsonProperty("Dimension2")]
		public Dimension1 Dimension2 { get; set; }

		[JsonProperty("Dimension3")]
		public Dimension1 Dimension3 { get; set; }

		[JsonProperty("Dimension4")]
		public Dimension1 Dimension4 { get; set; }

		[JsonProperty("Dimension5")]
		public Dimension1 Dimension5 { get; set; }

		[JsonProperty("Dimension6")]
		public Dimension1 Dimension6 { get; set; }

		[JsonProperty("Dimension7")]
		public Dimension1 Dimension7 { get; set; }

		[JsonProperty("Dimension8")]
		public Dimension1 Dimension8 { get; set; }

		[JsonProperty("Dimension9")]
		public Dimension1 Dimension9 { get; set; }

		[JsonProperty("Dimension10")]
		public Dimension1 Dimension10 { get; set; }

		[JsonProperty("Dimension11")]
		public Dimension1 Dimension11 { get; set; }

		[JsonProperty("Dimension12")]
		public Dimension1 Dimension12 { get; set; }

		[JsonProperty("FreeTextDimension1")]
		public Dimension1 FreeTextDimension1 { get; set; }

		[JsonProperty("FreeTextDimension2")]
		public Dimension1 FreeTextDimension2 { get; set; }

		[JsonProperty("FreeTextDimension3")]
		public Dimension1 FreeTextDimension3 { get; set; }

		[JsonProperty("FreeTextDimension4")]
		public Dimension1 FreeTextDimension4 { get; set; }

		[JsonProperty("FreeTextDimension5")]
		public Dimension1 FreeTextDimension5 { get; set; }

		[JsonProperty("UseTaxCodes")]
		public bool UseTaxCodes { get; set; }

		[JsonProperty("UseTaxCodesOnHead")]
		public bool UseTaxCodesOnHead { get; set; }

		[JsonProperty("UseTwoTaxIndicators")]
		public bool UseTwoTaxIndicators { get; set; }

		[JsonProperty("UsePaymentDetails")]
		public bool UsePaymentDetails { get; set; }

		[JsonProperty("TaxIndicator1Label")]
		public string TaxIndicator1Label { get; set; }

		[JsonProperty("TaxIndicator2Label")]
		public string TaxIndicator2Label { get; set; }

		[JsonProperty("TaxTolerance")]
		public object TaxTolerance { get; set; }

		[JsonProperty("DefaultCodingAmountConfiguration")]
		public DefaultCodingAmountConfiguration[] DefaultCodingAmountConfiguration { get; set; }

		[JsonProperty("OpenPeriodConfiguration")]
		public object[] OpenPeriodConfiguration { get; set; }
	}

	public partial class DefaultCodingAmountConfiguration
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("DocumentType")]
		public string DocumentType { get; set; }

		[JsonProperty("IsGrossEditableByDefault")]
		public bool IsGrossEditableByDefault { get; set; }
	}

	public partial class Dimension1
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("ShortName")]
		public string ShortName { get; set; }

		[JsonProperty("Active")]
		public bool Active { get; set; }

		[JsonProperty("MaximumLength")]
		public object MaximumLength { get; set; }
	}

	public partial class Supplier
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("SupplierId")]
		public string SupplierId { get; set; }

		[JsonProperty("Address")]
		public Address Address { get; set; }

		[JsonProperty("PaymentTerm")]
		public object PaymentTerm { get; set; }

		[JsonProperty("DeliveryTerm")]
		public object DeliveryTerm { get; set; }

		[JsonProperty("Currency")]
		public object Currency { get; set; }

		[JsonProperty("Identifiers")]
		public object[] Identifiers { get; set; }

		[JsonProperty("Company")]
		public object Company { get; set; }

		[JsonProperty("PreCoding")]
		public object PreCoding { get; set; }

		[JsonProperty("IsProcurable")]
		public bool IsProcurable { get; set; }

		[JsonProperty("TaxGroup")]
		public object TaxGroup { get; set; }

		[JsonProperty("TaxIndicator1")]
		public object TaxIndicator1 { get; set; }

		[JsonProperty("TaxIndicator2")]
		public object TaxIndicator2 { get; set; }

		[JsonProperty("PaymentDetails")]
		public object[] PaymentDetails { get; set; }

		[JsonProperty("IsActive")]
		public bool IsActive { get; set; }

		[JsonProperty("ExternalSystemId")]
		public string ExternalSystemId { get; set; }

		[JsonProperty("CustomFields")]
		public CustomFields CustomFields { get; set; }

		[JsonProperty("ImportedTimestamp")]
		public string ImportedTimestamp { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Address
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("StreetAddress")]
		public string StreetAddress { get; set; }

		[JsonProperty("City")]
		public string City { get; set; }

		[JsonProperty("Zip")]
		public string Zip { get; set; }

		[JsonProperty("Country")]
		public string Country { get; set; }

		[JsonProperty("Telephone")]
		public string Telephone { get; set; }

		[JsonProperty("Fax")]
		public string Fax { get; set; }

		[JsonProperty("Homepage")]
		public string Homepage { get; set; }

		[JsonProperty("Email")]
		public string Email { get; set; }
	}

	public partial class TransactionRate
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Currency")]
		public Currency Currency { get; set; }

		[JsonProperty("StartDate")]
		public string StartDate { get; set; }

		[JsonProperty("EndDate")]
		public string EndDate { get; set; }

		[JsonProperty("Company")]
		public object Company { get; set; }

		[JsonProperty("Value")]
		public long Value { get; set; }

		[JsonProperty("Number")]
		public Number Number { get; set; }

		[JsonProperty("DisplayValue")]
		public long DisplayValue { get; set; }

		[JsonProperty("Resolution")]
		public long Resolution { get; set; }

		[JsonProperty("IsActive")]
		public bool IsActive { get; set; }

		[JsonProperty("ExternalSystemId")]
		public string ExternalSystemId { get; set; }

		[JsonProperty("CustomFields")]
		public CustomFields CustomFields { get; set; }

		[JsonProperty("ImportedTimestamp")]
		public object ImportedTimestamp { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Number
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Value")]
		public long Value { get; set; }

		[JsonProperty("Resolution")]
		public long Resolution { get; set; }
	}

	public partial class VoucherObject
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Vouchers")]
		public Voucher[] Vouchers { get; set; }

		[JsonProperty("LatestVoucherNumber")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long LatestVoucherNumber { get; set; }

		[JsonProperty("PreliminaryPostingDate")]
		public object PreliminaryPostingDate { get; set; }

		[JsonProperty("FinalPostingDate")]
		public string FinalPostingDate { get; set; }

		[JsonProperty("PreliminaryVoucherNumber")]
		public string PreliminaryVoucherNumber { get; set; }

		[JsonProperty("InvalidatePostingDate")]
		public object InvalidatePostingDate { get; set; }

		[JsonProperty("InvalidateVoucherNumber")]
		public string InvalidateVoucherNumber { get; set; }

		[JsonProperty("FinalVoucherNumber")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long? FinalVoucherNumber { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Voucher
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("Value")]
		[JsonConverter(typeof(ParseStringConverter))]
		public long Value { get; set; }

		[JsonProperty("Tag")]
		public string Tag { get; set; }

		[JsonProperty("Date")]
		public string Date { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class ResponsePerspective
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("ReadOnly")]
		public bool ReadOnly { get; set; }

		[JsonProperty("Messages")]
		public string[] Messages { get; set; }

		[JsonProperty("WorkflowId")]
		public long WorkflowId { get; set; }

		[JsonProperty("ValidationErrors")]
		public object ValidationErrors { get; set; }
	}

	public partial class WorkflowContext
	{
		[JsonProperty("$type")]
		public string Type { get; set; }

		[JsonProperty("EntityVersion")]
		public long EntityVersion { get; set; }

		[JsonProperty("Id")]
		public long Id { get; set; }

		[JsonProperty("ViewId")]
		public Guid ViewId { get; set; }

		[JsonProperty("CreatedTimestamp")]
		public string CreatedTimestamp { get; set; }

		[JsonProperty("HashFiles")]
		public object[] HashFiles { get; set; }

		[JsonProperty("IsDeleted")]
		public bool IsDeleted { get; set; }

		[JsonProperty("ReferencedFilesCount")]
		public long ReferencedFilesCount { get; set; }
	}

	public partial class Response
	{
		public static Response FromJson(string json) => JsonConvert.DeserializeObject<Response>(json, Converter.Settings);
	}

	public static class Serialize
	{
		public static string ToJson(this Response self) => JsonConvert.SerializeObject(self, Converter.Settings);
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
}