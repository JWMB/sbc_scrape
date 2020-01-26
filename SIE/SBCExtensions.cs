using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SIE
{
	public static class SBCExtensions
	{
		public static void PreProcessCompanyName(this TransactionRecord tx)
		{
			var replacement = CompanyNameCustomReplacement(tx.CompanyName);
			if (replacement != tx.CompanyName)
				tx.CompanyName = replacement;
			else
			{
				var rx = new Regex(@"^(P?\d+)(?::)(.+)"); //"12345:abcde" or "P12345:abcde"
				var m = rx.Match(tx.CompanyName);
				if (m.Success)
				{
					tx.CompanyId = m.Groups[1].Value;
					tx.CompanyName = m.Groups[2].Value;
				}
				else
				{
					rx = new Regex(@"([^;]+);(.+)"); //SBC\slltbq 160905;CompanyName
					m = rx.Match(tx.CompanyName);
					if (m.Success)
					{
						tx.CompanyId = m.Groups[1].Value;
						tx.CompanyName = m.Groups[2].Value.Trim();
					}
				}
			}
		}

		static string CompanyNameCustomReplacement(string name)
		{
			//TODO: appSettings regex list?

			//TODO: special - RV:1SBC Sv Bostadsrättscentrum.
			//Maybe do a final pass for unmatched and find matches based on amount then match long-enough substrings of CompanyName?
			if (name.StartsWith("RV:1"))
				return name.Substring(4);

			//TODO: how to handle company name changes?
			if (name.Contains("Fortum Markets AB"))
				return "Ellevio AB";
			else if (name.Contains("Fortum Värme"))
				return "Stockholm Exergi";

			return name;
		}

	}
}
