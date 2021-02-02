using MediusFlowAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using sbc_scrape;
using sbc_scrape.Joining;
using sbc_scrape.SBC;
using Scrape.IO.Storage;
using SIE;
using SIE.Matching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
		public async Task SbcInvoiceIntegrity()
		{
			var years = new[] { 2019, 2020 };

			var sieFolder = Tools.GetOutputFolder("SIE");
			var sieFiles = new DirectoryInfo(sieFolder).GetFiles($"output_*.se").Select(o => o.Name).Where(o => years.Any(p => o.Contains(p.ToString())));
			var sie = await SBCExtensions.ReadSIEFiles(sieFiles.Select(file => Tools.GetOutputFolder("SIE", file)));
			var vouchers = sie.SelectMany(o => o.Children).OfType<VoucherRecord>().ToList();

			{
				var slrsWithout24400 = vouchers.Where(o => o.VoucherType == VoucherType.SLR || o.VoucherType == VoucherType.TaxAndExpense).Where(o => o.Transactions.Any(t => t.AccountId == 24400) == false);
				Assert.IsFalse(slrsWithout24400.Any());
			}
			{
				// SLR/LB shouldn't have different companynames on transactions (only if shortened)
				// LR however has one for 24400 (person) and purpose on the other accounts
				foreach (var item in vouchers.Where(o => o.VoucherType == VoucherType.SLR || o.VoucherType == VoucherType.LB))
				{
					var names = item.Transactions.Select(o => o.CompanyName).Distinct().OrderBy(o => o.Length).ToList();
					for (int i = 1; i < names.Count(); i++)
						Assert.IsTrue(names[i].StartsWith(names[i - 1]));
				}
			}

			var lbs = vouchers.Where(o => o.VoucherType == VoucherType.LB).ToList();
			{
				// Integrity
				var lbNo24400 = lbs.Where(o => o.Transactions.Any(t => t.AccountId == 24400) == false);
				Assert.IsFalse(lbNo24400.Any());
			}


			var dir = Tools.GetOutputFolder("sbc_html");
			var sbcInvoices = new sbc_scrape.SBC.InvoiceSource().ReadAll(dir).Where(o => years.Contains(o.RegisteredDate.Year));

			var byVerNum = sbcInvoices.GroupBy(o => o.IdSLR).ToDictionary(g => g.Key, g => g.ToList());
			var differentLinksSameVerNum = byVerNum.Where(o => o.Value.Select(p => p.InvoiceLink).Distinct().Count() > 2);
			Assert.IsFalse(differentLinksSameVerNum.Any());

			var invoiceKeys = byVerNum.Select(o => o.Key).ToList();
			var slrs = vouchers.Where(o => o.VoucherType == VoucherType.SLR).ToList();
			var slrKeys = slrs.Select(o => o.Id).ToList();
			// All Invoices should exist as SLRs:
			Assert.IsFalse(invoiceKeys.Except(slrKeys).Any());
			// Note: Invoices correspondeing to SLRs are created only when payed, so no need to check the other way around
		}

		[TestMethod]
		public async Task MatchMediusFlowWithSIE__X()
		{
			var years = new[] { 2017, 2018, 2019, 2020 }; //
			var store = new FileSystemKVStore(Tools.GetOutputFolder());

			var invoiceStore = new SBCScan.InvoiceStore(store);
			var files = await invoiceStore.GetKeysParsed();
			var invoices = (await Task.WhenAll(files.Where(o => years.Contains(o.RegisteredDate.Year)).Select(async o => await invoiceStore.Get(o)).ToList())).ToList();


			var sieFolder = Tools.GetOutputFolder("SIE");
			var sieFiles = new DirectoryInfo(sieFolder).GetFiles($"output_*.se").Select(o => o.Name).Where(o => years.Any(p => o.Contains(p.ToString())));
			var sie = await SBCExtensions.ReadSIEFiles(sieFiles.Select(file => Tools.GetOutputFolder("SIE", file)));

			var dir = Tools.GetOutputFolder("sbc_html");
			var sbcInvoices = new InvoiceSource().ReadAll(dir).Where(o => years.Contains(o.RegisteredDate.Year)).ToList();

			var joined = JoinSbcSieMediusFlow.MatchMediusFlowWithSIE(years, invoices, sie.ToList(), sbcInvoices);

			using (var writer = new StringWriter())
			using (var csv = new CsvHelper.CsvWriter(writer, GetDefaultCsvConfig()))
			{
				csv.WriteRecords(joined);
				var str = writer.ToString();
			}

			CsvHelper.Configuration.CsvConfiguration GetDefaultCsvConfig()
			{
				var cultureInfo = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
				cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
				cultureInfo.NumberFormat.NumberGroupSeparator = "";
				var conf = new CsvHelper.Configuration.CsvConfiguration(cultureInfo)
				{
					Delimiter = "\t",
				};
				conf.TypeConverterCache.AddConverter<LocalDate>(new LocalTimeConverter());
				return conf;
			}
		}
		class LocalTimeConverter : CsvHelper.TypeConversion.ITypeConverter
		{
			public object ConvertFromString(string text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
			{
				throw new NotImplementedException();
			}

			public string ConvertToString(object value, CsvHelper.IWriterRow row, CsvHelper.Configuration.MemberMapData memberMapData)
			{
				return value == null ? "" : ((LocalDate)value).ToSimpleDateString();
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
