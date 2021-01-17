using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SIE.Tests
{
	public class Exports
	{
		int Round(int val, int insignificantDigits)
		{
			var exp = (int)Math.Pow(10, insignificantDigits);
			return val / exp * exp;
		}

		[Fact]
		public async Task ResultatRakningCsv()
		{
			var roots = await TestingTools.ReadSIEFiles();
			var all = roots.Select(root => {
				var period = root.Children.OfType<ReportPeriodRecord>().FirstOrDefault();
				if (period == null)
					throw new NullReferenceException("period");
				Assert.True(period != null);
				Debug.WriteLine(period.Start);
				var year = period.Start.Year;
				var res = root.Children.OfType<ResultRecord>();
				return new
				{
					Year = year,
					Results = res.GroupBy(o => Round(o.AccountId, 3)).Select(o => new { AccountId = o.Key, Amount = o.Sum(p => p.Amount) }).ToList()
				};
			}).ToList();

			var allAccounts = all.SelectMany(o => o.Results.Select(p => p.AccountId)).Distinct().OrderBy(o => o);

			var rows = new List<List<string>>();

			var years = all.Select(o => o.Year).OrderBy(o => o);
			var header = new List<string> { "" }.Concat(years.Select(o => o.ToString())).ToList();
			rows.Add(header);

			foreach (var accountId in allAccounts)
			{
				var row = new List<string>();
				row.Add($"{accountId}");
				rows.Add(row);
				foreach (var year in years)
				{
					var inYear = all.Single(o => o.Year == year);
					var found = inYear.Results.SingleOrDefault(o => o.AccountId == accountId);
					row.Add(found == null ? "0" : $"{(int)found.Amount}");
				}
			}

			var csv = string.Join("\n", rows.Select(r => string.Join("\t", r)));
		}

		private class AccountInfo
		{
			public string Name { get; set; } = string.Empty;
			public string Source { get; set; } = string.Empty;
		}

		[Fact]
		public async Task GetAccounts()
		{
			var roots = await TestingTools.ReadSIEFiles(new[] { "output_2016.se", "output_2017.se", "output_2018.se" });
			var result = roots.SelectMany(o => o.Children.OfType<AccountRecord>()).GroupBy(o => o.AccountId).Select(o => o.First()).ToDictionary(o => o.AccountId, o => new AccountInfo { Name = o.AccountName, Source = "SIE" });
			//string.Join("\n",  Select(o => $"{o.AccountId}\t{o.AccountName}"));

			var sieDir = Path.Join(TestingTools.GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			var tmp = File.ReadAllText(Path.Combine(sieDir, "accountsexport.txt"));
			var xx = tmp.Split('\n').Skip(1).Where(o => o.Length > 0).Select(line => line.Split('\t')).ToDictionary(o => int.Parse(o[0]), o => o[1].Trim());

			foreach (var kv in xx)
			{
				if (!result.ContainsKey(kv.Key))
					result.Add(kv.Key, new AccountInfo { Name = $"{kv.Value}", Source = "" });
				else
				{
					result[kv.Key].Source = "";
					if (result[kv.Key].Name != kv.Value)
						result[kv.Key].Name += $" - {kv.Value}";
				}
			}

			var str = string.Join("\n", result.Keys.OrderBy(o => o).Select(o => $"{o}\t{result[o].Name}\t{result[o].Source}"));
		}
	}
}
