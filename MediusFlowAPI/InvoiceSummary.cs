﻿using MediusFlowAPI.Models.SupplierInvoiceGadgetData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MediusFlowAPI
{
	public class InvoiceSummary
	{
		public long Id { get; set; }
		public long? TaskId { get; set; }
		public DateTime? InvoiceDate { get; set; }
		public DateTime? CreatedDate { get; set; }
		public DateTime? DueDate { get; set; }
		public DateTime? FinalPostingingDate { get; set; }
		public long? TaskState { get; set; }
		public decimal GrossAmount { get; set; }
		public string Supplier { get; set; }
		public string SupplierId { get; set; }
		public long? AccountId { get; set; }
		public string AccountName { get; set; }
		public double? VAT { get; set; }
		//public string InvoiceType { get; set; }
		public string Houses
		{
			get
			{
				return string.Join(", ",
					RxFindHouse.Select(rx => rx.Matches(Comments))
					.Where(ms => ms.Count > 0).SelectMany(ms => ms.Cast<Match>().ToList()).Select(m => m.Groups["house"].Value)
					.Distinct().OrderBy(s => s));
			}
		}
		static List<Regex> _rxFindHouse;
		static List<Regex> RxFindHouse
		{
			get
			{
				if (_rxFindHouse == null)
					_rxFindHouse = new List<Regex> {
						new Regex(@"(Riksrådsv(ägen|\.)?|RRV|rrv|(n|N)r)\.?\s?(?<house>\d{2,3})"),
						new Regex(@"(?<house>\d{2,3})\:an"),
					};
				return _rxFindHouse;
			}
		}

		public List<string> InvoiceImageIds { get; set; }
		public string Comments { get; set; }
		public string History { get; set; }
		public string InvoiceTexts { get; set; }

		static readonly System.Globalization.CultureInfo Culture = new System.Globalization.CultureInfo("en-US");
		static readonly Regex rxSimplifyAuthor = new Regex(@"(?<name>(\w+\s){1,2})\s?\((\d{5,6}|SYSTEM)\)");

		public override string ToString()
		{
			return $"{InvoiceDate?.ToShortDateString()} {Supplier} {GrossAmount}";
		}

		public static string ShortenComments(string comments)
		{
			if (string.IsNullOrWhiteSpace(comments?.Trim()))
				return comments;

			var namePattern = @"(?<firstname>[\w]+)(?<middlenames>\s[\w]+){0,}(?<lastname>\s[\w]+)";
			var idPattern = @"(?<pid>\s?\(\d+\)\s*)(?!\(\d{2}-\d{2})";
			return new Regex(namePattern + idPattern).Replace(comments, "${firstname}${lastname}");
			// First Middle Middle Last (1234567) (12-18 10:51):Projektadministration / konsultation,First Last (1234568) (12-23 13:57):Möte med anbudsgivare - OK!
			//var rx = new Regex(@"(?<pid>\s?\(\d+\)\s*)(?!\(\d{2}-\d{2})");
			//return rx.Replace(comments, "");
		}

		public static InvoiceSummary Summarize(InvoiceFull invoice, string invoiceTexts = null)
		{
			var iv = invoice.Invoice;
			var ft = invoice.FirstTask;
			var task = ft?.Task;
			var taskDoc = task?.Document;
			var accountingDimension1 = invoice.Accounting?.DimensionStrings?.FirstOrDefault().Dimensions?.Dimension1;

			if (iv.FinalBookingDate.FromMediusDate() != iv.InvoiceDate.FromMediusDate())
			{ }
			if (iv.ActualPaymentDate.FromMediusDate() != null)
			{ }
			//if (taskDoc?.CustomFields?.TextCustomField1 != "382" && !string.IsNullOrEmpty(taskDoc?.CustomFields?.TextCustomField1))
			//{ }

			static string SimplifyAuthor(string author) => rxSimplifyAuthor.Replace(author, m => m.Groups["name"].Value.Trim()).Trim();

			static string GetHistoryItemSummary(Models.TaskHistory.Response item)
			{
				var description = SimplifyAuthor(item.Description);
				var replacements = new Dictionary<string, string> {
					{ "Fakturan attesterades av", "Attest:" },
					{ "Svara utfördes av", "Svar:" },
					{ "Granskning skickad till", "Att granska" },
					{ "Granskning slutförd av", "Granskad:" },
					{ "Distribuera utfördes av", "Dist:" },
					{ "Granskning återtagen av", "Ogranskad:" }
				};
				foreach (var kv in replacements)
					description = description.Replace(kv.Key, kv.Value);
				return $"{item.Date?.FromMediusDate()?.ToString("MM-dd")}: {SimplifyAuthor(description)}";
			}

			static string GetCommentSummary(Models.Comment.Response c)
			{
				return $"{SimplifyAuthor(c.Author)} ({c.CreatedDate?.FromMediusDate()?.ToString("MM-dd HH:mm")}):{c.Text.Replace("\n", "")}";
			}

			static string Join(string separator, IEnumerable<string> strings) => strings == null ? "" : string.Join(separator, strings);

			static string RemoveFromEnd(string str, string toRemove) => str.EndsWith(toRemove) ? str.Remove(str.Length - toRemove.Length) : str;
			
			try
			{
				return new InvoiceSummary
				{
					Id = iv.Id,
					InvoiceDate = iv.InvoiceDate.FromMediusDate(),
					CreatedDate = taskDoc?.CreatedTimestamp.FromMediusDate()?.Date,
					FinalPostingingDate = taskDoc?.VoucherObject?.FinalPostingDate.FromMediusDate()?.Date,
					TaskState = task?.State,
					TaskId = task?.Id,
					GrossAmount = decimal.Parse(iv.GrossAmount.DisplayValue, System.Globalization.NumberStyles.Any, Culture),
					Supplier = iv.Supplier.Name,
					SupplierId = iv.Supplier.SupplierId,
					DueDate = iv.DueDate.FromMediusDate(),
					AccountId = accountingDimension1?.Value?.ValueValue,
					AccountName = RemoveFromEnd(accountingDimension1?.Value?.Description, " -E-"),
					VAT = taskDoc?.CustomFields.NumericCustomField1.Value,
					Comments = Join(",", ft?.Comments?.Select(c => GetCommentSummary(c))),
					History = Join(",", ft?.History?.Select(c => GetHistoryItemSummary(c))),
					InvoiceImageIds = task?.Document?.HashFiles?.Where(hf => hf.HashFileType == "InvoiceImage").Select(hf => hf.Hash.ToString()).ToList(),
					InvoiceTexts = invoiceTexts,
				};
			}
			catch (Exception ex)
			{
				throw new Exception($"{iv.Id} {iv.Supplier.Name} {iv.InvoiceDate}", ex);
			}
		}
	}
}
