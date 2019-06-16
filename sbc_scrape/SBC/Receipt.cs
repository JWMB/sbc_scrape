﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sbc_scrape.SBC
{
	public class ReceiptsSource : HtmlSource<Receipt>
	{
		public override string SavedFilePrefix => "Receipts";
		public override string UrlPath => "Portalen/Ekonomi/Revisor/Kvittoparm/";
		public override List<Receipt> Parse(string html) => ParseX(html).Cast<Receipt>().ToList();
		public List<Receipt> ParseX(string html)
		{
			var culture = new System.Globalization.CultureInfo("sv-SE");
			return HtmlSource<Receipt>.ParseDocument(html, r => new Receipt
			{
				//BG = r[0],
				Date = DateTime.Parse(r[1]),
				SupplierId = r[2],
				FromAccount = r[3],
				Amount = decimal.Parse(r[4], culture),
				Unknown = r[5],
				Supplier = r[6],
			});
		}
	}

	public class Receipt
	{
		//Bankgironummer	Betalningsdatum	LevID Ekonomi	Ocr	Belopp	Information
		public DateTime Date { get; set; }
		public string SupplierId { get; set; }
		public string FromAccount { get; set; }
		public decimal Amount { get; set; }
		public string Unknown { get; set; }
		public string Supplier { get; set; }

		public override string ToString()
		{
			return $"{Date.ToShortDateString()} {Amount} {SupplierId} {Supplier}";
		}
	}
}