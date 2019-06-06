using HtmlAgilityPack;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SBCScan;

namespace sbc_scrape
{
	public class Fakturaparm
	{
		public DateTime RegisteredDate { get; set; }
		public DateTime PaymentDate { get; set; }
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

		public class SBCvsMediusEqualityComparer : IEqualityComparer<MediusFlowAPI.InvoiceSummary>
		{
			private List<List<long>> mixedUpAccountIds = GlobalSettings.AppSettings.MixedUpAccountIdsParsed;

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
					&& (b1.AccountId == b2.AccountId || (GetIsMixedUpPair(b1.AccountId.Value, b2.AccountId.Value)));
			}

			public int GetHashCode(MediusFlowAPI.InvoiceSummary bx)
			{
				//Seems we can't trust accountId, but the first digit is correct
				return ($"{bx.AccountId.ToString().Remove(1)}{bx.GrossAmount}{(bx.InvoiceDate?.ToString("yyyy-MM-dd"))}{bx.Supplier}").GetHashCode(); //{bx.DueDate}
			}
		}

		public static List<Fakturaparm> ReadAll(string folder)
		{
			return new System.IO.DirectoryInfo(folder).GetFiles("*.html").SelectMany(file => Parse(System.IO.File.ReadAllText(file.FullName))).ToList();
		}

		public static List<Fakturaparm> Parse(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var accountNames = doc.DocumentNode.SelectNodes("(//table[contains(@class, 'portal-table-nogrid')]//select)[1]//option")
				.Select(n => new { Key = n.GetAttributeValue("value", 0), Value = HtmlEntity.DeEntitize(n.InnerText) })
				.ToDictionary(k => k.Key, k => k.Value.Replace($"{k.Key} ", ""));

			//"table class="portal-table"";
			var node = doc.DocumentNode.SelectSingleNode("/html[1]/body[1]/form[1]/div[6]/div[1]/div[5]/div[3]/div[2]/div[1]/div[2]/div[1]/table[1]");
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
			return parsedRows.Select(r => new Fakturaparm {
				RegisteredDate = DateTime.Parse(r[0]),
				PaymentDate = DateTime.Parse(r[1]),
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
