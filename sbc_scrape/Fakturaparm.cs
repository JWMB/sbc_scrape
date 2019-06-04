using HtmlAgilityPack;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace sbc_scrape
{
	public class Fakturaparm
	{
		public DateTime RegisteredDate { get; set; }
		public DateTime PaymentDate { get; set; }
		public string Supplier { get; set; }
		public string LevNr { get; set; }
		public int Account { get; set; }
		public decimal Amount { get; set; }
		public string InvoiceLink { get; set; }

		//.Parse(System.IO.File.ReadAllText(root + "sbc_scrape\\scraped\\sbc_fakturaparm\\2016.html"));

		public static List<Fakturaparm> ReadAll(string folder)
		{
			return new System.IO.DirectoryInfo(folder).GetFiles("*.html").SelectMany(file => Parse(System.IO.File.ReadAllText(file.FullName))).ToList();
		}

		public static List<Fakturaparm> Parse(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			//"table class="portal-table"";
			var node = doc.DocumentNode.SelectSingleNode("/html[1]/body[1]/form[1]/div[6]/div[1]/div[5]/div[3]/div[2]/div[1]/div[2]/div[1]/table[1]");
			var rows = node.ChildNodes.Where(n => n.Name == "tr");
			var headerRow = rows.First();
			var cells = headerRow.ChildNodes.Where(n => n.Name == "th");
			var columnNames = cells.Select(n => HtmlEntity.DeEntitize(n.FirstChild.InnerText)).ToList();

			var skipColumns = new string[] { "Ver serie", "Ver nr" };
			var skipColumnIndices = skipColumns.Select(c => columnNames.IndexOf(c)).Where(i => i >= 0).ToList();

			//var tds = rows.Skip(1).First().ChildNodes.Where(n => n.Name == "td");
			var parsedRows = rows.Skip(1).Select(r => r.ChildNodes.Where(n => n.Name == "td").ToList())
				.Select(row => row.Where((r, i) => !skipColumnIndices.Contains(i)).Select(c => {
					return c.FirstChild.Name == "a" ? c.FirstChild.GetAttributeValue("href", "") : HtmlEntity.DeEntitize(c.InnerText);
				}).ToList()).ToList();

			return parsedRows.Select(r => new Fakturaparm {
				RegisteredDate = DateTime.Parse(r[0]),
				PaymentDate = DateTime.Parse(r[1]),
				Supplier = r[2],
				LevNr = r[3],
				Account = int.Parse(r[4]),
				Amount = decimal.Parse(r[5]),
				InvoiceLink = r[6]
			}).ToList();

			//var result = new List<List<string>> {
			//	columnNames.Where((c, i) => !skipColumnIndices.Contains(i)).ToList()
			//};
			//result.AddRange(parsedRows);
			//return result;
		}
	}
}
