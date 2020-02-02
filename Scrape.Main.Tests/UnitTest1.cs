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
			var fromSBC = new List<SBCVariant>();
			{
				var dir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "sbc_html");
				var tmp = new List<sbc_scrape.SBC.Invoice>();
				await foreach (var sbcRows in new sbc_scrape.SBC.InvoiceSource().ReadAllAsync(dir))
					tmp.AddRange(sbcRows.Where(o => accountFilter(o.AccountId)));

				fromSBC = tmp.Select(o => new SBCVariant {
					AccountId = o.AccountId,
					Amount = o.Amount,
					CompanyName = o.Supplier,
					DateRegistered = NodaTime.LocalDate.FromDateTime(o.RegisteredDate),
					DateFinalized = o.PaymentDate.HasValue ? NodaTime.LocalDate.FromDateTime(o.PaymentDate.Value) : (NodaTime.LocalDate?)null,
					Source = o,
				}).ToList(); //fromSBC.OrderByDescending(o => o.RegisteredDate).ToList();
			}

			//Load SIE vouchers
			List<TransactionMatched> fromSIE; //List<MatchSLRResult.Matched> fromSIE;
			{
				var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
				var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
				var roots = await SBCExtensions.ReadSIEFiles(files.Select(file => Path.Combine(sieDir, file)));
				var allVouchers = roots.SelectMany(o => o.Children).Where(o => o is VoucherRecord).Cast<VoucherRecord>();

				var matchResult = MatchSLRResult.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);
				fromSIE = TransactionMatched.FromVoucherMatches(matchResult, TransactionMatched.RequiredAccountIds).Where(o => accountFilter(o.AccountId)).ToList();
			}

			var sbcByName = fromSBC.GroupBy(o => o.CompanyName).ToDictionary(o => o.Key, o => o.ToList());
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

			var matches = new List<(TransactionMatched, SBCVariant)>();
			foreach (var (sieName, sieList) in sieByName)
			{
				if (nameLookup.ContainsKey(sieName))
				{
					var inSbc = sbcByName[nameLookup[sieName]].GroupBy(o => o.Amount).ToDictionary(o => o.Key, o => o.ToList());

					//Multiple passes of the following until no more matches
					while (true)
					{
						var newMatches = new List<(TransactionMatched, SBCVariant)>();
						for (int sieIndex = sieList.Count - 1; sieIndex >= 0; sieIndex--) //foreach (var item in sieItem.Value)
						{
							var item = sieList[sieIndex];
							if (inSbc.TryGetValue(item.Amount, out var sbcSameAmount))
							{
								var sbcSameAmountAccount = sbcSameAmount.Where(o => o.AccountId == item.AccountId);
								//Find those with same register date (could be many)
								//If multiple or none, take those with closest payment date.
								//Remove match from inSbc so it can't be matched again
								var found = new List<SBCVariant>();
								if (item.DateRegistered.HasValue)
									found = sbcSameAmountAccount.Where(o => (o.DateRegistered.Value - item.DateRegistered.Value).Days <= 1).ToList();
								else
									found = sbcSameAmountAccount.Where(o => (o.DateFinalized.HasValue && item.DateFinalized.HasValue)
										&& (o.DateFinalized.Value - item.DateFinalized.Value).Days <= 1).ToList();

								if (found.Count > 1)
								{
									var orderByDateDiff = found
										.Where(o => (o.DateFinalized.HasValue && item.DateFinalized.HasValue))
										.Select(o => new
										{
											Diff = Math.Abs((o.DateFinalized.Value - item.DateFinalized.Value).Days),
											Object = o
										})
										.OrderBy(o => o.Diff);
									var minDiff = orderByDateDiff.First().Diff;
									if (orderByDateDiff.Count(o => o.Diff == minDiff) == 1)
									{
										found = orderByDateDiff.Take(1).Select(o => o.Object).ToList();
									}
								}
								if (found.Count == 1)
								{
									newMatches.Add((item, found.Single()));
									sieList.RemoveAt(sieIndex);
									sbcSameAmount.Remove(found.First());
								}
							}
						}
						if (!newMatches.Any())
							break;
						matches.AddRange(newMatches);
					}
				}
			}

			var nonmatched = sbcByName.Values.SelectMany(o => o).Except(matches.Select(o => o.Item2))
				.Concat(sieByName.Values.SelectMany(o => o).Except(matches.Select(o => o.Item1)));

			var all = matches.Select(o => o.Item1).Concat(nonmatched);

			var sss = string.Join("\n",
				all
					.OrderBy(o => o.CompanyName).ThenBy(o => o.DateRegistered)
					.Select(o => $"{o.CompanyName}\t{o.Amount}\t{o.AccountId}\t{o.DateRegistered?.ToSimpleDateString()}\t{o.DateFinalized?.ToSimpleDateString()}\t{(nonmatched.Contains(o) ? "X" : "")}\t{((o is SBCVariant) ? "SBC" : "")}")
			);
		}

		class SBCVariant : TransactionMatched
		{
			public sbc_scrape.SBC.Invoice Source { get; set; } = new sbc_scrape.SBC.Invoice();
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
