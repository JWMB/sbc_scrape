using HtmlAgilityPack;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SBCScan;

namespace sbc_scrape.SBC
{
	public class InvoiceSource : HtmlSource<Invoice>
	{
		public override string SavedFilePrefix => "Invoices";
		public override string UrlPath => "Portalen/Ekonomi/Utforda-betalningar/"; // Changed Aug 2020 from Portalen/Ekonomi/Fakturaparm/
		public override List<Invoice> Parse(string html) => ParseX(html).Cast<Invoice>().ToList();
		public List<Invoice> ParseX(string html)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var docVersion = GetDocumentVersion(html);

			var accountNameOptions = docVersion == DocumentVersion.Pre2020
				? doc.DocumentNode.SelectNodes("(//table[contains(@class, 'portal-table-nogrid')]//select)[1]//option")
				: (docVersion == DocumentVersion.Spring2020
					? doc.DocumentNode.SelectSingleNode("//select[@id='ctl00_MainBodyAddRegion_ctl01_DDKontoFrom']").ChildNodes.Where(o => o.Name == "option")
					: doc.DocumentNode.SelectSingleNode("//select[@id='ctl00_MainBodyAddRegion_ctl01_DDAccountFrom']").ChildNodes.Where(o => o.Name == "option")
					);

			var accountNames = accountNameOptions
					.Select(n => new { Key = n.GetAttributeValue("value", 0), Value = HtmlEntity.DeEntitize(n.InnerText) })
					.ToDictionary(k => k.Key, k => k.Value.Replace($"{k.Key} ", ""));

			var culture = new System.Globalization.CultureInfo("sv-SE");
			return ParseDocument(html, r =>
			{
				var colIndex = -1;
				return new Invoice
				{
					VerSeries = r[++colIndex].Trim(),
					VerNum = int.Parse(r[++colIndex]),
					RegisteredDate = DateTime.Parse(r[++colIndex]),
					PaymentDate = string.IsNullOrWhiteSpace(r[++colIndex]) ? (DateTime?)null : DateTime.Parse(r[colIndex]),
					Supplier = r[++colIndex],
					LevNr = r[++colIndex],
					AccountId = int.Parse(r[++colIndex]),
					AccountName = accountNames.GetValueOrDefault(int.Parse(r[colIndex]), "N/A"),
					Amount = ParseDecimal(r[++colIndex]),
					InvoiceLink = r[++colIndex]
				};
			}, new string[] { }).ToList();
		}
	}

	public class Invoice
	{
		public string VerSeries { get; set; }
		public int VerNum { get; set; }
		public DateTime RegisteredDate { get; set; }
		public DateTime? PaymentDate { get; set; }
		public string Supplier { get; set; }
		public string LevNr { get; set; }
		public int AccountId { get; set; }
		public string AccountName { get; set; }
		public decimal Amount { get; set; }
		public string InvoiceLink { get; set; }

		public override string ToString()
		{
			return $"{RegisteredDate.ToShortDateString()} {Amount} {AccountId} {Supplier}";
		}

		public MediusFlowAPI.InvoiceSummary ToSummary()
		{
			return new MediusFlowAPI.InvoiceSummary {
				Supplier = Supplier,
				GrossAmount = Amount,
				AccountId = AccountId,
				AccountName = AccountName,
				InvoiceDate = RegisteredDate,
				DueDate = PaymentDate,
				Comments = "",
				History = "",
				VAT = null,
				TaskId = null,
				Id = 0,
				TaskState = null,
				CreatedDate = null,
			};
		}

		public class InvoiceSummaryMismatch
		{
			public MediusFlowAPI.InvoiceSummary Summary { get; set; }
			public string Source { get; set; }
			public string Type { get; set; }
		}

		public static List<InvoiceSummaryMismatch> GetMismatchedEntries(
			IEnumerable<MediusFlowAPI.InvoiceSummary> mediusFlowSummaries,
			IEnumerable<MediusFlowAPI.InvoiceSummary> sbcSummaries)
		{
			//Find inconsistancies between two systems:
			var exactOverlap = sbcSummaries.Intersect(mediusFlowSummaries, new SBCvsMediusEqualityComparer(false));
			var fudgedOverlap = sbcSummaries.Intersect(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true));
			var fudgedOnly = fudgedOverlap.Except(exactOverlap);
			var fudgedFromMediusFlow = mediusFlowSummaries.Intersect(fudgedOnly, new SBCvsMediusEqualityComparer(true));
			var fudgedBothSources = fudgedOnly.Select(s => new InvoiceSummaryMismatch { Summary = s, Source = "SBC" })
				.Concat(fudgedFromMediusFlow.Select(s => new InvoiceSummaryMismatch { Summary = s, Source = "MFw" }))
				.ToList();

			fudgedBothSources.ForEach(item => { item.Type = "Acct"; });

			var notInMediusFlow = sbcSummaries.Where(s => s.InvoiceDate > new DateTime(2016, 4, 1))
				.Except(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true));

			return fudgedBothSources.Concat(notInMediusFlow.Select(s => new InvoiceSummaryMismatch {
				Summary = s, Source = "SBC", Type = "Miss" })).OrderByDescending(s => s.Summary.InvoiceDate).ToList();
		}

		public static List<MediusFlowAPI.InvoiceSummary> Join(IEnumerable<MediusFlowAPI.InvoiceSummary> mediusFlowSummaries,
			IEnumerable<MediusFlowAPI.InvoiceSummary> sbcSummaries)
		{
			var comparer = new SBCvsMediusEqualityComparer(true);
			var onlyInMediusFlow = mediusFlowSummaries.Except(sbcSummaries, comparer);
			var onlyInSbc = sbcSummaries.Except(mediusFlowSummaries, comparer);

			var joined = mediusFlowSummaries.Join(sbcSummaries, s => s, s => s, (mf, sbc) => {
				mf.AccountId = sbc.AccountId;
				//mf.DueDate = sbc.DueDate;
				return mf;
				}, comparer);

			var all = onlyInMediusFlow.Concat(onlyInSbc).Concat(joined);
			return all.OrderByDescending(o => o.InvoiceDate).ToList();

			////Use "real" data instead of Fakturaparm where they overlap:
			//var duplicatesRemoved = sbcSummaries.Except(mediusFlowSummaries, new SBCvsMediusEqualityComparer(true))
			//	.Concat(mediusFlowSummaries);
			//return duplicatesRemoved.OrderByDescending(o => o.InvoiceDate).ToList();
		}

		public class SBCvsMediusEqualityComparer : IEqualityComparer<MediusFlowAPI.InvoiceSummary>
		{
			private readonly List<List<long>> mixedUpAccountIds = GlobalSettings.AppSettings.MixedUpAccountIdsParsed;
			private readonly bool matchMixedUpAccounts;

			public SBCvsMediusEqualityComparer(bool matchMixedUpAccounts = true)
			{
				this.matchMixedUpAccounts = matchMixedUpAccounts;
			}

			private bool GetIsMixedUpPair(long id1, long id2) => mixedUpAccountIds.FirstOrDefault(pair => pair.Contains(id1) && pair.Contains(id2)) != null;

			public bool Equals(MediusFlowAPI.InvoiceSummary b1, MediusFlowAPI.InvoiceSummary b2)
			{
				if (b2 == null && b1 == null)
					return true;
				else if (b1 == null || b2 == null)
					return false;
				else return 
					b1.GrossAmount == b2.GrossAmount
					&& b1.InvoiceDate == b2.InvoiceDate
					//&& b1.DueDate == b2.DueDate 
					//These can have wildly different dates though same invoice - see 2016-12-22_Just Nu Malmö_2938517_01-16_2.json
					//SBC Fakturaparm says DueDate = 2017-01-18, MediusFlow says 2017-01-01
					&& b1.Supplier == b2.Supplier
					&& (b1.AccountId == b2.AccountId || (matchMixedUpAccounts && (GetIsMixedUpPair(b1.AccountId.Value, b2.AccountId.Value))));
			}

			public int GetHashCode(MediusFlowAPI.InvoiceSummary bx)
			{
				//Seems we can't trust accountId, but the first digit is correct
				return ($"{bx.AccountId.ToString().Remove(1)}{bx.GrossAmount}{(bx.InvoiceDate?.ToString("yyyy-MM-dd"))}{bx.Supplier}").GetHashCode(); //{bx.DueDate}
			}
		}
	}
}
