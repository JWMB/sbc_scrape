using MediusFlowAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIE;
using SIE.Matching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scrape.Main.Tests
{
	[TestClass]
	public class UnitTest1
	{
		private string GetOutputFolder()
		{
			return Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped");
			//var folder = Environment.CurrentDirectory;
			//var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
			//if (folder.Contains(bin))
			//	folder = folder.Remove(folder.LastIndexOf(bin));
			//return Path.Combine(folder, "scraped");
		}

		private T LoadJsonFromFile<T>(string path) where T : JToken
		{
			path = Path.Combine(GetOutputFolder(), path);
			if (!File.Exists(path))
				throw new FileNotFoundException(path);
			var json = File.ReadAllText(path);
			if (json.Length == 0)
				throw new FileLoadException($"File empty: {path}");
			var token = JToken.Parse(json);
			if (token is T)
				return token as T;
			throw new Exception($"JSON in '{path}' is not a {typeof(T).Name}");
		}

		[TestMethod]
		public async Task TestMethod1()
		{
			Func<int, bool> accountFilter = accountId => accountId.ToString().StartsWith("45");

			//Load SBC invoices
			var fromSBC = new List<sbc_scrape.SBC.Invoice>();
			{
				var dir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "sbc_html");
				await foreach (var sbcRows in new sbc_scrape.SBC.InvoiceSource().ReadAllAsync(dir))
					fromSBC.AddRange(sbcRows.Where(o => accountFilter(o.AccountId)));

				fromSBC = fromSBC.OrderByDescending(o => o.RegisteredDate).ToList();
			}

			//Load SIE vouchers
			List<MatchSLRResult.Matched> fromSIE;
			{
				var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
				var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
				var roots = await SBCExtensions.ReadSIEFiles(files.Select(file => Path.Combine(sieDir, file)));
				var allVouchers = roots.SelectMany(o => o.Children).Where(o => o is VoucherRecord).Cast<VoucherRecord>();

				var matchResult = MatchSLRResult.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);

				fromSIE = matchResult.Matches.Where(o => o.AccountIdNonAdmin.ToString().StartsWith("45")).ToList();
			}

			//fromSIE.First().Other.Date

			var sbcByName = fromSBC.GroupBy(o => o.Supplier).ToDictionary(o => o.Key, o => o.ToList());
			var sieByName = fromSIE.GroupBy(o => o.CompanyName).ToDictionary(o => o.Key, o => o.ToList());

			//Create name lookup (can be truncated in one source but not the other):
			Dictionary<string, string> nameLookup;
			{
				var intersectInfo = IntersectInfo(sbcByName.Keys, sieByName.Keys);
				nameLookup = intersectInfo.Intersection.ToDictionary(o => o, o => o);

				AddLookups(intersectInfo.OnlyInA, intersectInfo.OnlyInB);
				AddLookups(intersectInfo.OnlyInB, intersectInfo.OnlyInA);
				void AddLookups(List<string> enumA, List<string> enumB)
				{
					for (int i = enumA.Count - 1; i >= 0; i--)
					{
						var itemA = enumA[i];
						var itemB = enumB.FirstOrDefault(o => o.StartsWith(itemA));
						if (itemB != null)
						{
							enumB.Remove(itemB);
							enumA.Remove(itemA);
							nameLookup.Add(itemB, itemA);
							nameLookup.Add(itemA, itemB);
						}
					}
				}
				//Non-matched: intersectInfo.OnlyInA and intersectInfo.OnlyInB
			}

			var dbg = "";
			foreach (var sieItem in sieByName)
			{
				if (nameLookup.ContainsKey(sieItem.Key))
				{
					var inSbc = sbcByName[nameLookup[sieItem.Key]];
					var ss = inSbc.GroupBy(o => o.Amount).ToDictionary(o => o.Key, o => o.ToList());

					foreach (var item in sieItem.Value)
					{
						var amount = Math.Abs(item.SLR.TransactionsNonAdminOrCorrections.FirstOrDefault()?.Amount ?? 0M);
						//if (amount == 0)
						//	continue;
						if (ss.TryGetValue(amount, out var xxx))
						{
							dbg += $"{item.CompanyName} {amount} {item.Other.Date.ToSimpleDateString()} {xxx.Count}\n";
						}
						else
						{
							dbg += $"{item.CompanyName} {amount} {item.Other.Date.ToSimpleDateString()}\n";
						}
					}
				}
				else; //Ignore non-matched for now
			}

			//var dbg = string.Join('\n', x1.Concat(x2));
			//var dbg = string.Join("\n", maintenance.OrderByDescending(o => o.Other.Date).Select(o =>
			//	$"{o.Other.Date.ToSimpleDateString()}\t{o.SLR.Date.ToSimpleDateString()}\t{o.AccountIdNonAdmin}\t{o.SLR.TransactionsNonAdminOrCorrections.First().Amount}\t{o.SLR.Transactions.First().CompanyName}"));
		}

		(List<T> Intersection, List<T> OnlyInA, List<T> OnlyInB) IntersectInfo<T>(IEnumerable<T> enumA, IEnumerable<T> enumB)
		{
			var intersect = enumA.Intersect(enumB).ToList();
			return (intersect, enumA.Except(intersect).ToList(), enumB.Except(intersect).ToList());
		}

		void DownloadManagement(List<sbc_scrape.SBC.Invoice> fromSBC)
		{

			var downloadDir = new DirectoryInfo(Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "maintenance"));
			var rxFilename = new System.Text.RegularExpressions.Regex(@"\/([^\/]+\.\w{3})$");
			var unnamedCnt = 0;
			var problems = new List<string>();
			fromSBC.ToList().ForEach(o => {
				var m = rxFilename.Match(o.InvoiceLink);
				string filename;
				if (m.Success)
					filename = System.Net.WebUtility.UrlDecode(m.Groups[1].Value);
				else
					filename = $"Invoice({++unnamedCnt}).pdf";

				if (!File.Exists(Path.Join(downloadDir.FullName, filename)))
				{
					problems.Add($"N/A  {o.InvoiceLink}");
					return;
				}
				var name = $"{o.RegisteredDate:yyyy-MM-dd}_{(int)Math.Abs(o.Amount)}_{Truncate(o.Supplier, 7)}_{filename}";
				File.Copy(Path.Join(downloadDir.FullName, filename), Path.Join(downloadDir.FullName, "Renamed/", name), true);
			});

			string Truncate(string val, int maxLength) { if (val.Length > maxLength) return val.Remove(maxLength); return val; }
			var script = @"
function download(all) {
	let cnt = 0;
	const handle = setInterval(() => {
		window.open(all[cnt], '_blank');
		cnt++;
		if (cnt == all.length - 1) { clearInterval(handle); }
	}, 1000);
}
download([{links}]);
/*
{info}
*/
";
			script = script.Replace("{links}", string.Join("\n", fromSBC.Select(o => $"'{o.InvoiceLink.Replace("\\", "\\\\")}',")));
			script = script.Replace("{info}", string.Join("\n", fromSBC.Select(o => $"{o.RegisteredDate.ToString("yyyy-MM-dd")}\t{o.Amount}\t{o.Supplier}\t{o.InvoiceLink}")));
			//PaymentDate
		}

		string GetCurrentOrSolutionDirectory()
		{
			var sep = "\\" + Path.DirectorySeparatorChar;
			var rx = new System.Text.RegularExpressions.Regex($@".*(?={sep}[^{sep}]+{sep}bin)");
			var m = rx.Match(Directory.GetCurrentDirectory());
			return m.Success ? m.Value : Directory.GetCurrentDirectory();
		}

	}
}
