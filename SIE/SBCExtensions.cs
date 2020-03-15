using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIE
{
	public static class SBCExtensions
	{
		static readonly List<Regex> CompanyNameRx = new List<Regex> {
			new Regex(@"^(P?\d+)(?::)(.+)"),
			new Regex(@"([^;]+);(.+)"),
			new Regex(@"^(\d{2})\/(.+)"),
		};

		public static void PreProcessCompanyName(this TransactionRecord tx)
		{
			var replacement = CompanyNameCustomReplacement(tx.CompanyName);
			if (replacement != tx.CompanyName)
				tx.CompanyName = replacement;
			else
			{
				foreach (var rx in CompanyNameRx)
				{
					var m = rx.Match(tx.CompanyName);
					if (m.Success)
					{
						tx.CompanyId = m.Groups[1].Value;
						tx.CompanyName = m.Groups[2].Value.Trim();
						break;
					}
				}
			}
		}
		public static void PostProcessCompanyName(this TransactionRecord tx)
		{
			if (tx.CompanyName == "Blp Skyddsrum AB")
				tx.CompanyName = "BLP Entreprenad AB";
			else if (tx.CompanyName == "Byggrevision Fastighet i Stock")
				tx.CompanyName = "Byggrevision Sverige AB";
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


		public static async Task<List<RootRecord>> ReadSIEFiles(IEnumerable<string> files)
		{
			var tasks = files.Select(async file => await SIERecord.Read(file));
			await Task.WhenAll(tasks);
			var result = tasks.Select(o => o.Result).ToList();

			var vouchers = result.SelectMany(o => o.Children).OfType<VoucherRecord>();
			vouchers.SelectMany(o => o.Transactions).ToList().ForEach(o => o.PreProcessCompanyName());

			VoucherRecord.NormalizeCompanyNames(vouchers);

			vouchers.SelectMany(o => o.Transactions).ToList().ForEach(o => o.PostProcessCompanyName());

			return result;
		}

	}
}
