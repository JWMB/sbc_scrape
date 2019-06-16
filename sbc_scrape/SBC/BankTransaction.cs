using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sbc_scrape.SBC
{
	class BankTransactionSource : HtmlSource<BankTransaction>
	{
		public override string SavedFilePrefix => "Transactions";
		public override string UrlPath => "Portalen/Ekonomi/Revisor/Kontoutdrag/";
		public override List<BankTransaction> Parse(string html) => ParseX(html).Cast<BankTransaction>().ToList();
		public List<BankTransaction> ParseX(string html)
		{
			var culture = new System.Globalization.CultureInfo("sv-SE");
			return ParseDocument(html, r => new BankTransaction
			{
				Reference = r[0],
				AccountingDate = DateTime.Parse(r[1]),
				CurrencyDate = DateTime.Parse(r[2]),
				Text = r[3],
				Amount = decimal.Parse(r[4], culture),
				TotalAccountAmount = decimal.Parse(r[5], culture),
			});
		}
	}
	public class BankTransaction
	{
		public string Reference { get; set; }
		public DateTime AccountingDate { get; set; }
		public DateTime CurrencyDate { get; set; }
		public string Text { get; set; }
		public decimal Amount { get; set; }
		public decimal TotalAccountAmount { get; set; }
	}
}
