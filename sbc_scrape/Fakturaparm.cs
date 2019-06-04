using HtmlAgilityPack;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace sbc_scrape
{
	public class Fakturaparm
	{
		public static void Parse(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			//"table class="portal-table"";
			var node = doc.DocumentNode.SelectSingleNode("/html[1]/body[1]/form[1]/div[6]/div[1]/div[5]/div[3]/div[2]/div[1]/div[2]/div[1]/table[1]");
			//node.ChildNodes.Select(n => n.ToString());
		}
	}
}
