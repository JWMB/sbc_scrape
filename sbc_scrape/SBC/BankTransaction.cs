using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sbc_scrape.SBC
{
	public class BankTransaction
	{
		public string Reference { get; set; }
		public DateTime AccountingDate { get; set; }
		public DateTime CurrencyDate { get; set; }
		public string Text { get; set; }
		public decimal Amount { get; set; }
		public decimal TotalAccountAmount { get; set; }

		public static string FilenamePattern => "Transactions_{0}.html";
		public static List<BankTransaction> ReadAll(string folder)
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

		public static List<BankTransaction> Parse(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var node = doc.DocumentNode.SelectSingleNode("//table[@class='portal-table']");
			if (node == null)
				throw new FormatException("Couldn't find table node");
			node = node.ChildNodes.FirstOrDefault(n => n.Name == "tbody") ?? node; //Some variants have no tbody, but tr directly under

			var rows = node.ChildNodes.Where(n => n.Name == "tr");
			var headerRow = rows.First();
			var cells = headerRow.ChildNodes.Where(n => n.Name == "th");
			var columnNames = cells.Select(n => HtmlEntity.DeEntitize(n.FirstChild.InnerText)).ToList();

			var skipColumns = new string[] { };
			var skipColumnIndices = skipColumns.Select(c => columnNames.IndexOf(c)).Where(i => i >= 0).ToList();

			var parsedRows = rows.Skip(1).Select(r => r.ChildNodes.Where(n => n.Name == "td").ToList())
				.Select(row => row.Where((r, i) => !skipColumnIndices.Contains(i)).Select(c => {
					return c.FirstChild.Name == "a" ? c.FirstChild.GetAttributeValue("href", "") : HtmlEntity.DeEntitize(c.InnerText);
				}).ToList()).ToList();

			var culture = new System.Globalization.CultureInfo("sv-SE");
			return parsedRows.Select(r => new BankTransaction
			{
				Reference = r[0],
				AccountingDate = DateTime.Parse(r[1]),
				CurrencyDate = DateTime.Parse(r[2]), //string.IsNullOrWhiteSpace(r[1]) ? (DateTime?)null : DateTime.Parse(r[1]),
				Text = r[3],
				Amount = decimal.Parse(r[4], culture),
				TotalAccountAmount = decimal.Parse(r[5], culture),
			}).ToList();
		}
	}
}
