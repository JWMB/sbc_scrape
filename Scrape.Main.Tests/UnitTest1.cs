using MediusFlowAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using sbc_scrape;
using Scrape.IO.Storage;
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

		InvoiceFull CreateInvoiceFull(DateTime date, string supplier)
		{
			return new InvoiceFull
			{
				Invoice = new MediusFlowAPI.Models.SupplierInvoiceGadgetData.Invoice
				{
					Id = 1,
					InvoiceDate = date.ToMediusDate(),
					Supplier = new MediusFlowAPI.Models.SupplierInvoiceGadgetData.Supplier {
						Name = supplier
					}
				},
				TaskAssignments = new List<InvoiceFull.TaskAssignmentAndTasks> {
					new InvoiceFull.TaskAssignmentAndTasks {
						Task = new InvoiceFull.TaskFull {
							Task = new MediusFlowAPI.Models.Task.Response {
								CreatedTimestamp =  date.ToMediusDate(),
								State = 2
							}
						} }
				}
			};
		}

		[TestMethod]
		public async Task ScrapedStartEndDates()
		{
			var store = new InMemoryKVStore();
			var invoiceStore = new SBCScan.InvoiceStore(store);

			await invoiceStore.Post(CreateInvoiceFull(new DateTime(2017, 1, 1), "Supplier"));
			var alreadyScraped = await invoiceStore.GetKeysParsed();

			var mf = new SBCScan.MediusFlow(store, null);
			var tmp = await mf.GetStartEndDates(alreadyScraped, null, null, false);
		}

		[TestMethod]
		public void ParsePdf()
		{
			//TODO: this test (and assembly reference) should not be in this test project!!
			var file = @"C:\Users\Jonas Beckeman\Downloads\document(2).pdf";
			var texts = OCR.PdfExtract.Extract(file);
		}

		[TestMethod]
		public async Task MyTestMethod()
		{
			var store = new FileSystemKVStore(Tools.GetOutputFolder());

			var invoiceStore = new SBCScan.InvoiceStore(store);
			var alreadyScraped = await invoiceStore.GetKeysParsed();

			var errors = new Dictionary<InvoiceFull.FilenameFormat, Exception>();
			foreach (var item in alreadyScraped)
			{
				try
				{
					await invoiceStore.Get(item);
				}
				catch (Exception ex)
				{
					errors.Add(item, ex);
				}
			}
			Assert.IsFalse(errors.Any());
		}

		[TestMethod]
		public async Task MatchMediusFlowWithSIE()
		{
			var year = 2020;
			var store = new FileSystemKVStore(Tools.GetOutputFolder());

			var invoiceStore = new SBCScan.InvoiceStore(store);
			var files = await invoiceStore.GetKeysParsed();
			var invoices = (await Task.WhenAll(files.Where(o => o.RegisteredDate.Year == year).Select(async o => await invoiceStore.Get(o)).ToList())).ToList();
			invoices = invoices.Where(o => o.IsRejected == false).ToList();

			var summaries = invoices.Select(o => InvoiceSummary.Summarize(o)).Where(o => o.InvoiceDate.Value.Year == year).ToList();

			var sieFolder = Tools.GetOutputFolder("SIE");
			var sieFile = new DirectoryInfo(sieFolder).GetFiles($"output_{year}*.se").First().Name;
			var sie = await SBCExtensions.ReadSIEFiles(new[] { sieFile }.Select(file => Tools.GetOutputFolder("SIE", file)));
			var vouchers = sie.SelectMany(o => o.Children).OfType<VoucherRecord>().ToList();

			//var accountChanged = vouchers.Where(o => o.VoucherType == VoucherType.SLR)
			//	.Where(o => o.Transactions.GroupBy(t => t.AccountId).Any(t => t.Select(tt => Math.Sign(tt.Amount)).Distinct().Count() > 1))
			//	.ToList();

			var result = InvoiceSIEMatch.MatchLB_SLR(vouchers, summaries);

			var missing = result.Matches.Where(o => o.SLR.Voucher == null).ToList();
			//Assert.IsFalse(missing.Any());

			// Some urgent invoices go (by request) direct to payment, without passing MediusFlow (e.g. 2020 "Office for design", "Stenbolaget")
			// The same goes for SBCs own periodical invoices and bank/interest payments
			// These should be found here: https://varbrf.sbc.se/Portalen/Ekonomi/Utforda-betalningar/
			// Actually, this is just a view for SLR records - with an additional link for the invoice
			var tmp1 = result.UnmatchedSLR;
			var tmp2 = result.UnmatchedSLRShouldHaveOtherTrail.ToList();
			var tmp = result.Matches.Where(o => !o.MatchedAllExpected).ToList();


			var dir = Tools.GetOutputFolder("sbc_html");
			var sbcInvoices = new sbc_scrape.SBC.InvoiceSource().ReadAll(dir).Where(o => o.RegisteredDate.Year == year);
			// check integrity
			{
				var byVerNum = sbcInvoices.GroupBy(o => $"{year}_{o.VerNum}").ToDictionary(g => g.Key, g => g.ToList());
				var differentLinksSameVerNum = byVerNum.Where(o => o.Value.Select(p => p.InvoiceLink).Distinct().Count() > 2);
				Assert.IsFalse(differentLinksSameVerNum.Any());

				var invoiceKeys = byVerNum.Select(o => o.Key).ToList();
				var slrs = vouchers.Where(o => o.VoucherType == VoucherType.SLR).ToList();
				var slrKeys = slrs.Select(o => $"{year}_{o.SerialNumber}").ToList();
				// All Invoices should exist as SLRs:
				Assert.IsFalse(invoiceKeys.Except(slrKeys).Any());
				// SLRs don't exist as Invoice until payed, so no need to check the other way around
			}

			string GetProperSN(VoucherRecord v) => $"{v.Date.Year}_{v.SerialNumber}";
			var matchesBySLR = result.Matches.Select(m => new { Key = GetProperSN(m.SLR.Voucher), Value = m }).ToDictionary(o => o.Key, o => o.Value);
			var fullResult = vouchers.Where(o => o.VoucherType == VoucherType.SLR).Select(slr =>
			{
				var sbcInv = sbcInvoices.FirstOrDefault(o => o.RegisteredDate.Year == slr.Date.Year && o.VerNum == slr.SerialNumber);
				var info = new InvoiceInfo { SLR = slr, SbcImageLink = sbcInv?.InvoiceLink };
				if (matchesBySLR.TryGetValue(GetProperSN(slr), out var found))
				{
					info.Invoice = found.Invoice;
					info.LB = found.LB.Voucher;
				}
				return info;
			}).ToList();

			// https://varbrf.sbc.se/Portalen/Ekonomi/Revisor/Underlagsparm/
		}

		public class InvoiceInfo
		{
			public VoucherRecord SLR { get; set; }
			public VoucherRecord? LB { get; set; }
			public InvoiceSummary? Invoice { get; set; }
			public string? SbcImageLink { get; set; }

			public override string ToString()
			{
				return $"{SLR.Date.ToSimpleDateString()} {SLR.CompanyName} {SLR.GetTransactionsMaxAmount()} {(Invoice == null ? "" : "INV")} {(SbcImageLink == null ? "" : "IMG")}";
			}
		}

		[TestMethod]
		public async Task TestMethod1()
		{
			Func<int, bool> accountFilter = accountId => accountId.ToString().StartsWith("45");

			//Load SBC invoices
			var fromSBC = (await Tools.LoadSBCInvoices(accountFilter)).Select(o => new SBCVariant {
					AccountId = o.AccountId,
					Amount = o.Amount,
					CompanyName = o.Supplier,
					DateRegistered = NodaTime.LocalDate.FromDateTime(o.RegisteredDate),
					DateFinalized = o.PaymentDate.HasValue ? NodaTime.LocalDate.FromDateTime(o.PaymentDate.Value) : (NodaTime.LocalDate?)null,
					Source = o,
				}).ToList();

			//Load SIE vouchers
			List<TransactionMatched> fromSIE;
			{
				var files = Enumerable.Range(2010, 10).Select(o => $"output_{o}.se");
				var sieDir = Tools.GetOutputFolder("SIE");
				var roots = await SBCExtensions.ReadSIEFiles(files.Select(file => Path.Combine(sieDir, file)));
				var allVouchers = roots.SelectMany(o => o.Children).OfType<VoucherRecord>();

				var matchResult = MatchSLRResult.MatchSLRVouchers(allVouchers, VoucherRecord.DefaultIgnoreVoucherTypes);
				fromSIE = TransactionMatched.FromVoucherMatches(matchResult, TransactionMatched.RequiredAccountIds).Where(o => accountFilter(o.AccountId)).ToList();
			}

			var sbcByName = fromSBC.GroupBy(o => o.CompanyName).ToDictionary(o => o.Key, o => o.ToList());
			var sieByName = fromSIE.GroupBy(o => o.CompanyName).ToDictionary(o => o.Key, o => o.ToList());

			//Create name lookup (can be truncated in one source but not the other):
			Dictionary<string, string> nameLookup;
			{
				var (Intersection, OnlyInA, OnlyInB) = IntersectInfo(sbcByName.Keys, sieByName.Keys);
				nameLookup = Intersection.ToDictionary(o => o, o => o);

				AddLookups(OnlyInA, OnlyInB);
				AddLookups(OnlyInB, OnlyInA);
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
						for (int sieIndex = sieList.Count - 1; sieIndex >= 0; sieIndex--)
						{
							var item = sieList[sieIndex];
							if (inSbc.TryGetValue(item.Amount, out var sbcSameAmount))
							{
								var sbcSameAmountAccount = sbcSameAmount.Where(o => o.AccountId == item.AccountId);
								//Find those with same register date (could be many)
								//If multiple or none, take those with closest payment date.
								//Remove match from inSbc so it can't be matched again
								var found = new List<SBCVariant>();
								if (item.DateRegistered is NodaTime.LocalDate dateRegistered)
									found = sbcSameAmountAccount.Where(o => (dateRegistered - item.DateRegistered.Value).Days <= 1).ToList();
								else
									found = sbcSameAmountAccount.Where(o => (o.DateFinalized.HasValue && item.DateFinalized.HasValue)
										&& (o.DateFinalized.Value - item.DateFinalized.Value).Days <= 1).ToList();

								if (found.Count > 1)
								{
									var orderByDateDiff = found
										.Where(o => (o.DateFinalized.HasValue && item.DateFinalized.HasValue))
										.Select(o => new
										{
#pragma warning disable CS8629 // Nullable value type may be null.
											Diff = Math.Abs((o.DateFinalized.Value - item.DateFinalized.Value).Days),
#pragma warning restore CS8629 // Nullable value type may be null.
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

			var nonmatchedSBC = sbcByName.Values.SelectMany(o => o).Except(matches.Select(o => o.Item2));
			//Remove cancelled-out pairs (same everything but opposite amount):
			var cancelling = nonmatchedSBC.GroupBy(o => $"{o.CompanyName} {Math.Abs(o.Amount)} {o.DateRegistered?.ToSimpleDateString()} {o.DateFinalized?.ToSimpleDateString()}")
				.Where(o => o.Count() == 2 && o.Sum(o => o.Amount) == 0).SelectMany(o => o);
			nonmatchedSBC = nonmatchedSBC.Except(cancelling);

			var nonmatched = nonmatchedSBC.Concat(sieByName.Values.SelectMany(o => o).Except(matches.Select(o => o.Item1)));

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


		[TestMethod]
		public async Task DownloadedGetAmounts()
		{
			var invoices = await Tools.LoadSBCInvoices(accountId => accountId.ToString().StartsWith("45"));
			//DownloadManagement(invoices);
			var filenames = GenerateFilenames(invoices);
			var sorted = string.Join("\n", filenames.OrderBy(o => o.extendedFilename).Select(o => $"{o.extendedFilename}\t{o.invoice.Amount}\t{o.invoice.AccountId}"));
		}

		static List<(sbc_scrape.SBC.Invoice invoice, string filename, string extendedFilename)> GenerateFilenames(List<sbc_scrape.SBC.Invoice> fromSBC)
		{
			var rxFilename = new System.Text.RegularExpressions.Regex(@"\/([^\/]+\.\w{3})$");
			var unnamedCnt = 0;
			return fromSBC.Select(o => {
				var m = rxFilename.Match(o.InvoiceLink);
				string filename;
				if (m.Success)
					filename = System.Net.WebUtility.UrlDecode(m.Groups[1].Value);
				else
					filename = $"Invoice({++unnamedCnt}).pdf";

				return (
					o,
					filename,
					$"{o.RegisteredDate:yyyy-MM-dd}_{(int)Math.Abs(o.Amount)}_{Truncate(o.Supplier, 7)}_{filename}"
				);
			}).ToList();

			static string Truncate(string val, int maxLength) { if (val.Length > maxLength) return val.Remove(maxLength); return val; }
		}
		void DownloadManagement(List<sbc_scrape.SBC.Invoice> fromSBC)
		{
			var downloadDir = new DirectoryInfo(Path.Join(Tools.GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "maintenance"));
			var filenames = GenerateFilenames(fromSBC);
			var problems = new List<string>();
			foreach (var (invoice, filename, extendedFilename) in filenames)
			{
				if (!File.Exists(Path.Join(downloadDir.FullName, filename)))
				{
					problems.Add($"N/A  {invoice.InvoiceLink}");
					continue;
				}
				File.Copy(Path.Join(downloadDir.FullName, filename), Path.Join(downloadDir.FullName, "Renamed/", extendedFilename), true);
			}

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
			script = script.Replace("{info}", string.Join("\n", fromSBC.Select(o => $"{o.RegisteredDate:yyyy-MM-dd}\t{o.Amount}\t{o.Supplier}\t{o.InvoiceLink}")));
			//PaymentDate
		}
	}
}
