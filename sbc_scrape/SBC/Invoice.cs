using HtmlAgilityPack;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SBCScan;

namespace sbc_scrape.SBC
{
	public class Invoice
	{
		public DateTime RegisteredDate { get; set; }
		public DateTime? PaymentDate { get; set; }
		public string Supplier { get; set; }
		public string LevNr { get; set; }
		public int AccountId { get; set; }
		public string AccountName { get; set; }
		public decimal Amount { get; set; }
		public string InvoiceLink { get; set; }

		public MediusFlowAPI.InvoiceSummary ToSummary()
		{
			return new MediusFlowAPI.InvoiceSummary {
				Supplier = Supplier,
				GrossAmount = Amount,
				AccountId = AccountId,
				AccountName = AccountName,
				InvoiceDate = RegisteredDate,
				DueDate = PaymentDate,
				Comments = "",
				History = "",
				VAT = null,
				TaskId = null,
				Id = 0,
				TaskState = null,
				CreatedDate = null,
			};
		}

		public class InvoiceSummaryMismatch
		{
			public MediusFlowAPI.InvoiceSummary Summary { get; set; }
			public string Source { get; set; }
			public string Type { get; set; }
		}

		public static List<InvoiceSummaryMismatch> GetMismatchedEntries(
			IEnumerable<MediusFlowAPI.InvoiceSummary> mediusFlowSummaries,
			IEnumerable<MediusFlowAPI.InvoiceSummary> sbcSummaries)
		{
			//Find inconsistancies between two systems:
			var exactOverlap = sbcSummaries.Intersect(mediusFlowSummaries, new SBCvsMediusEqualityComparer(false));
			var fudgedOverlap = sbcSummaries.Intersect(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true));
			var fudgedOnly = fudgedOverlap.Except(exactOverlap);
			var fudgedFromMediusFlow = mediusFlowSummaries.Intersect(fudgedOnly, new SBCvsMediusEqualityComparer(true));
			var fudgedBothSources = fudgedOnly.Select(s => new InvoiceSummaryMismatch { Summary = s, Source = "SBC" })
				.Concat(fudgedFromMediusFlow.Select(s => new InvoiceSummaryMismatch { Summary = s, Source = "MFw" }))
				.ToList();

			fudgedBothSources.ForEach(item => { item.Type = "Acct"; });

			var notInMediusFlow = sbcSummaries.Where(s => s.InvoiceDate > new DateTime(2016, 4, 1))
				.Except(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true));

			return fudgedBothSources.Concat(notInMediusFlow.Select(s => new InvoiceSummaryMismatch {
				Summary = s, Source = "SBC", Type = "Miss" })).OrderByDescending(s => s.Summary.InvoiceDate).ToList();
		}

		public static List<MediusFlowAPI.InvoiceSummary> Join(IEnumerable<MediusFlowAPI.InvoiceSummary> mediusFlowSummaries,
			IEnumerable<MediusFlowAPI.InvoiceSummary> sbcSummaries)
		{
			var comparer = new SBCvsMediusEqualityComparer(true);
			var onlyInMediusFlow = mediusFlowSummaries.Except(sbcSummaries, comparer);
			var onlyInSbc = sbcSummaries.Except(mediusFlowSummaries, comparer);

			var joined = mediusFlowSummaries.Join(sbcSummaries, s => s, s => s, (mf, sbc) => {
				mf.AccountId = sbc.AccountId;
				//mf.DueDate = sbc.DueDate;
				return mf;
				}, comparer);

			var all = onlyInMediusFlow.Concat(onlyInSbc).Concat(joined);
			return all.OrderByDescending(o => o.InvoiceDate).ToList();

			////Use "real" data instead of Fakturaparm where they overlap:
			//var duplicatesRemoved = sbcSummaries.Except(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true))
			//	.Concat(mediusFlowSummaries);
			//return duplicatesRemoved.OrderByDescending(o => o.InvoiceDate).ToList();
		}

		public class SBCvsMediusEqualityComparer : IEqualityComparer<MediusFlowAPI.InvoiceSummary>
		{
			private List<List<long>> mixedUpAccountIds = GlobalSettings.AppSettings.MixedUpAccountIdsParsed;
			private readonly bool matchMixedUpAccounts;

			public SBCvsMediusEqualityComparer(bool matchMixedUpAccounts = true)
			{
				this.matchMixedUpAccounts = matchMixedUpAccounts;
			}

			private bool GetIsMixedUpPair(long id1, long id2) => mixedUpAccountIds.FirstOrDefault(pair => pair.Contains(id1) && pair.Contains(id2)) != null;

			public bool Equals(MediusFlowAPI.InvoiceSummary b1, MediusFlowAPI.InvoiceSummary b2)
			{
				if (b2 == null && b1 == null)
					return true;
				else if (b1 == null || b2 == null)
					return false;
				else return 
					b1.GrossAmount == b2.GrossAmount
					&& b1.InvoiceDate == b2.InvoiceDate
					//&& b1.DueDate == b2.DueDate 
					//These can have wildly different dates though same invoice - see 2016-12-22_Just Nu Malmö_2938517_01-16_2.json
					//SBC Fakturaparm says DueDate = 2017-01-18, MediusFlow says 2017-01-01
					&& b1.Supplier == b2.Supplier
					&& (b1.AccountId == b2.AccountId || (matchMixedUpAccounts && (GetIsMixedUpPair(b1.AccountId.Value, b2.AccountId.Value))));
			}

			public int GetHashCode(MediusFlowAPI.InvoiceSummary bx)
			{
				//Seems we can't trust accountId, but the first digit is correct
				return ($"{bx.AccountId.ToString().Remove(1)}{bx.GrossAmount}{(bx.InvoiceDate?.ToString("yyyy-MM-dd"))}{bx.Supplier}").GetHashCode(); //{bx.DueDate}
			}
		}

		public static string FilenamePattern => "Invoices_{0}.html";
		public static List<Invoice> ReadAll(string folder)
		{
			return new System.IO.DirectoryInfo(folder).GetFiles(string.Format(FilenamePattern, "*"))
				.SelectMany(file => {
					try
					{
						return Parse(System.IO.File.ReadAllText(file.FullName));
					}
					catch (Exception ex)
					{
						throw new FormatException($"{ex.Message} for '{file.FullName}'", ex);
					}
				}).ToList();
		}

		public static List<Invoice> Parse(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var accountNames = doc.DocumentNode.SelectNodes("(//table[contains(@class, 'portal-table-nogrid')]//select)[1]//option")
				.Select(n => new { Key = n.GetAttributeValue("value", 0), Value = HtmlEntity.DeEntitize(n.InnerText) })
				.ToDictionary(k => k.Key, k => k.Value.Replace($"{k.Key} ", ""));

			var node = doc.DocumentNode.SelectSingleNode("//table[@class='portal-table']");
			if (node == null)
				throw new FormatException("Couldn't find table node");
			node = node.ChildNodes.FirstOrDefault(n => n.Name == "tbody") ?? node; //Some variants have no tbody, but tr directly under

			var rows = node.ChildNodes.Where(n => n.Name == "tr");
			var headerRow = rows.First();
			var cells = headerRow.ChildNodes.Where(n => n.Name == "th");
			var columnNames = cells.Select(n => HtmlEntity.DeEntitize(n.FirstChild.InnerText)).ToList();

			var skipColumns = new string[] { "Ver serie", "Ver nr" };
			var skipColumnIndices = skipColumns.Select(c => columnNames.IndexOf(c)).Where(i => i >= 0).ToList();

			var parsedRows = rows.Skip(1).Select(r => r.ChildNodes.Where(n => n.Name == "td").ToList())
				.Select(row => row.Where((r, i) => !skipColumnIndices.Contains(i)).Select(c => {
					return c.FirstChild.Name == "a" ? c.FirstChild.GetAttributeValue("href", "") : HtmlEntity.DeEntitize(c.InnerText);
				}).ToList()).ToList();

			var culture = new System.Globalization.CultureInfo("sv-SE");
			return parsedRows.Select(r => new Invoice {
				RegisteredDate = DateTime.Parse(r[0]),
				PaymentDate = string.IsNullOrWhiteSpace(r[1]) ? (DateTime?)null : DateTime.Parse(r[1]),
				Supplier = r[2],
				LevNr = r[3],
				AccountId = int.Parse(r[4]),
				AccountName = accountNames.GetValueOrDefault(int.Parse(r[4]), "N/A"),
				Amount = decimal.Parse(r[5], System.Globalization.NumberStyles.Any, culture), //.Replace(",", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator)),
				InvoiceLink = r[6]
			}).ToList();
		}
	}
}
