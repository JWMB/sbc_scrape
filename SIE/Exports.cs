using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIE
{
	public class Exports
	{
		public static string ResultatRakningCsv(IEnumerable<RootRecord> sie)
		{
			var resultsByYear = sie.Select(s => new
			{
				Year = s.Children.OfType<ReportPeriodRecord>().Single().Start.Year,
				Results = s.Children.OfType<ResultRecord>().ToList()
			}).ToDictionary(o => o.Year, o => o.Results);

			if (false) // NOPE, prolly not correct
			{
				// Seems like maybe "BS" voucher is some kind of revision/correction? Only valid until "finalized" (but not indication of this in file..?)
				var lastYear = DateTime.Today.Year - 1;
				var lastYearSie = sie.FirstOrDefault(s => s.Children.OfType<ReportPeriodRecord>().Single().Start.Year == lastYear);
				if (lastYearSie != null)
				{
					var corrections = lastYearSie.Children.OfType<VoucherRecord>().Where(v => v.VoucherType == VoucherType.BS).ToList();
					var summed = corrections.SelectMany(o => o.TransactionsNonAdminOrCorrections).GroupBy(o => o.AccountId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
					foreach (var kv in summed)
						resultsByYear[lastYear].Add(new ResultRecord { AccountId = kv.Key, Amount = -kv.Value });
				}
			}

			var accountIdToName = sie.SelectMany(o => o.Children.OfType<AccountRecord>())
				.GroupBy(o => o.AccountId)
				.Select(o => new { o.Key, o.First().AccountName })
				.ToDictionary(o => o.Key, o => o.AccountName);

			var grouping = new
			{
				Income = new
				{
					Revenue = "^3[^9]",
					Split = new { Coop = "30110", Rent = "^302(1|3)", Parking = "30251" },
					Other = "^39",
					Sum = "^3",
				},
				Costs = new
				{
					OperatingCosts = new
					{
						Property = "^41",
						Repairs = "^43",
						Maintenance = "^45",
						Repeating = "^46",
						OperatingCosts = "^47",
						Sum = "^4[^8]",
					},
					PropertyTax = "^48",
					SumOpAndTac = "^4",
					Other = "^6",
					Personnel = "^7[^8]",
					Depreciation = "^78",
					Sum = "^[467]",
				},
				ResultPreFinancials = "^(3|4|6|7)",
				Financial = new
				{
					IncomingInterest = "^83",
					OutgoingInterest = "^841", //842..? 89991?
					Sum = "^8(3|41)",
					Unknown = "^8(?!(3|41))"
				},
				ResultPostFinancials = "^(3|4|6|7|83|841)",
				ResultPreFinExDepreciation = "^(3|4|6|7[^8])",
				ResultPostFinExDepreciation = "^(3|4|6|7[^8]|83|841)",
				IncomeTax = "^89100",
			};

			var translation = new Dictionary<string, string> {
				{ "Income", "Rörelseintäkter" },
				{ "Income:Revenue", "Nettoomsättning" },
				{ "Income:Split", "" },
				{ "Income:Split:Coop", "Årsavgifter" },
				{ "Income:Split:Rent", "Hyror" },
				{ "Income:Split:Parking", "Parkering" },
				{ "Income:Other", "Övriga rörelseintäkter" },
				{ "Income:Sum", "Summa rörelseintäkter" },
				{ "Costs", "Rörelsekostnader" },
				{ "Costs:OperatingCosts", "Driftskostnader" },
				{ "Costs:OperatingCosts:Property", "Fastighetskostnader" },
				{ "Costs:OperatingCosts:Repairs", "Reparationer" },
				{ "Costs:OperatingCosts:Maintenance", "Periodiskt underhåll" },
				{ "Costs:OperatingCosts:Repeating", "Taxebundna kostnader" },
				{ "Costs:OperatingCosts:OperatingCosts", "Driftskostnader" },
				{ "Costs:OperatingCosts:Sum", "Summa Driftskostnader" },
				{ "Costs:PropertyTax", "Fastighetsskatt" },
				{ "Costs:SumOpAndTac", "Summa drift+skatt" },
				{ "Costs:Other", "Övriga externa kostnader" },
				{ "Costs:Personnel", "Personalkostnader" },
				{ "Costs:Depreciation", "Avskrivning av materiella anläggningstillgångar" },
				{ "Costs:Sum", "Summa rörelsekostnader" },
				{ "ResultPreFinancials", "RESULTAT FÖRE FINANSIELLA POSTER" },
				{ "Financial", "Finansiella poster" },
				{ "Financial:IncomingInterest", "Övriga ränteintäkter och liknande resultatposter" },
				{ "Financial:OutgoingInterest", "Räntekostnader och liknande resultatposter" },
				{ "Financial:Sum", "Summa finansiella poster" },
				{ "ResultPostFinancials", "RESULTAT EFTER FINANSIELLA POSTER" },
				{ "ResultPreFinExDepreciation", "" },
				{ "ResultPostFinExDepreciation", "Resultat ex. avskriningar" },
				{ "IncomeTax", "Statlig inkomstskatt" }
			};

			var tableExplanation = new List<List<string>>();
			tableExplanation.Add(new[] { "Post", "Accounts" }.ToList());
			GetX(grouping, new Stack<string>(), (path, value) => {
				var pathStr = string.Join(":", path.Reverse());
				var translated = translation.GetValueOrDefault(pathStr, pathStr);
				var rx = value is string rxStr ? new Regex(rxStr) : null;

				tableExplanation.Add(new List<string> {
					string.Join("", Enumerable.Repeat(". ", path.Count - 1)) + translated,
					value is string str ? str : "",
					rx == null ? "" : string.Join(",", accountIdToName.Where(kv => rx.IsMatch(kv.Key.ToString())).Select(kv => kv.Value))
				});
			});

			var table = new List<List<string>>();
			foreach (var kv in resultsByYear.OrderByDescending(o => o.Key))
			{
				var row = new List<string>();
				table.Add(row);
				row.Add(kv.Key.ToString());
				var accounts = kv.Value;
				GetX(grouping, new Stack<string>(), (path, value) => {
					decimal? sum = null;
					if (value is string)
					{
						var rx = new Regex((string)value);
						var selected = accounts.Where(o => rx.IsMatch(o.AccountId.ToString())).ToList();
						sum = selected.Sum(o => o.Amount);
					}
					row.Add($"{(sum.HasValue ? (-1 * Math.Round(sum.Value)).ToString() : "")}");
				});
			}
			table = Rotate90(table);
			for (int i = 0; i < tableExplanation.Count; i++)
			{
				var r = tableExplanation[i];
				table[i] = r.Take(1).Concat(table[i]).Concat(r.Skip(1)).ToList(); // Explanation: Text first, other columns last
			}

			return string.Join("\n", table.Select(o1 => string.Join("\t", o1)));

			void GetX(object obj, Stack<string> path, Action<Stack<string>, object> action)
			{
				var type = obj.GetType();
				var props = type.GetProperties();
				foreach (var prop in props)
				{
					var val = prop.GetValue(obj);
					if (val == null)
						continue;

					path.Push(prop.Name);
					if (val is string)
						action(path, (string)val);
					else
					{
						action(path, null);
						GetX(val, path, action);
					}
					path.Pop();
				}
			}
		}

		public static List<List<T>> Rotate90<T>(IEnumerable<IEnumerable<T>> grid)
		{
			var result = new List<List<T>>();
			var maxRowLen = grid.Max(o => o.Count());
			for (int i = 0; i < maxRowLen; i++)
				result.Add(new List<T>());

			foreach (var r in grid)
			{
				var i = 0;
				foreach (var val in r)
				{
					result[i].Add(val);
					i++;
				}
			}
			return result;
		}

	}
}
