﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediusFlowAPI
{
	public class InvoiceFull
	{
		public class TaskAssignmentAndTasks
		{
			public Models.TaskAssignment TaskAssignment { get; set; }
			public TaskFull Task { get; set; }
		}
		public class TaskFull
		{
			public Models.Task.Response Task { get; set; }
			public Models.TaskHistory.Response[] History { get; set; }
			public Models.Comment.Response[] Comments { get; set; }
			public Dictionary<Guid, object> Images { get; set; }
		}
		public Models.SupplierInvoiceGadgetData.Invoice Invoice { get; set; }
		public Models.AccountingObjectWithLinesForInvoice.Response Accounting { get; set; }
		public List<TaskAssignmentAndTasks> TaskAssignments { get; set; } = new List<InvoiceFull.TaskAssignmentAndTasks>();


		[JsonIgnore()]
		public TaskFull FirstTask { get => TaskAssignments?.FirstOrDefault()?.Task; }

		public override string ToString()
		{
			return $"{(Invoice.InvoiceDate.FromMediusDate()?.ToString("yyyy-MM-dd") ?? "0000")} {Invoice.Supplier.Name} {Invoice.GrossAmount.DisplayValue} {Invoice.Id}";
		}

		static string MakeSafeFilename(string str, int truncate = 0)
		{
			foreach (char c in System.IO.Path.GetInvalidFileNameChars())
				if (str.Contains(c))
					str = str.Replace(c, '_');
			return truncate > 0 && str.Length > truncate ? str.Remove(truncate) : str;
		}

		public static string GetFilenamePrefix(DateTime invoiceDate, string supplier, long invoiceId)
		{
			return string.Join("_",
				invoiceDate.ToString("yyyy-MM-dd"),
				MakeSafeFilename(supplier, 15),
				invoiceId.ToString());
		}

		public class FilenameFormat
		{
			public DateTime InvoiceDate { get; set; }
			public long Id { get; set; }
			public string Supplier { get; set; }
			public DateTime RegisteredDate { get; set; }
			public long? State { get; set; }

			public override string ToString()
			{
				return GetFilenamePrefix(InvoiceDate, Supplier, Id) + string.Join("_",
					RegisteredDate.ToString("MM-dd"),
					State.ToString()
					);
			}
			public static string Create(InvoiceFull invoice)
			{
				var registeredDate = invoice.TaskAssignments.Min(ta => ta.Task.Task.CreatedTimestamp.FromMediusDate());
				//TODO: key should be the registered date?
				var fmt = new FilenameFormat
				{
					InvoiceDate = invoice.Invoice.InvoiceDate.FromMediusDate() ?? DateTime.MinValue,
					Supplier = invoice.Invoice.Supplier.Name,
					Id = invoice.Invoice.Id,
					RegisteredDate = registeredDate ?? DateTime.MinValue,
					State = invoice.FirstTask?.Task?.State
				};

				return fmt.ToString();
			}
			public static FilenameFormat Parse(string filename)
			{
				var split = filename.Split('_');
				var invoiceDate = DateTime.Parse(split[0]);
				var status = 0;
				var lastIndex = split.Length - 1;
				if (split.Length > 4)
				{
					status = int.Parse(split[lastIndex]);
					lastIndex--;
				}
				var registered = split[lastIndex];
				var rMonth = int.Parse(registered.Split('-')[0]);
				var registeredDate = DateTime.Parse("" + (rMonth < invoiceDate.Month ? invoiceDate.Year + 1 : invoiceDate.Year)
					+ "-" + registered);

				return new FilenameFormat
				{
					InvoiceDate = invoiceDate,
					Supplier = split[1],
					Id = long.Parse(split[lastIndex - 1]),
					RegisteredDate = registeredDate,
					State = status,
				};
			}
		}
	}
}
