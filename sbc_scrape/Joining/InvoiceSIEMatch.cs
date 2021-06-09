using MediusFlowAPI;
using NodaTime;
using SIE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sbc_scrape.Joining
{
	public class InvoiceMatch
	{
		public InvoiceSummary Invoice { get; set; }
		public VoucherOrReason LB { get; set; }
		public VoucherOrReason SLR { get; set; }

		public string SbcInvoiceLink { get; set; }
		public bool MatchedAllExpected { get => LB.FoundOrValidReason && SLR.FoundOrValidReason; }

		public InvoiceMatch(InvoiceSummary invoice, VoucherOrReason lb, VoucherOrReason slr)
		{
			Invoice = invoice;
			LB = lb;
			SLR = slr;
		}

		public override string ToString()
		{
			return $"{Invoice} {MatchedAllExpected} {(MatchedAllExpected ? "" : $"LB:{LB.MissingReason} SLR:{SLR.MissingReason}")}";
		}
	}

	public enum VoucherMissingReason
	{
		Unknown,
		DateRange,
		InvoiceNotProcessed
	}

	public struct VoucherOrReason
	{
		public VoucherRecord? Voucher { get; private set; }
		public VoucherMissingReason? MissingReason { get; private set; }
		public bool FoundOrValidReason { get => Voucher != null || MissingReason != VoucherMissingReason.Unknown; }
		public VoucherOrReason(VoucherRecord voucher)
		{
			Voucher = voucher;
			MissingReason = null;
		}
		public VoucherOrReason(VoucherMissingReason reason)
		{
			Voucher = null;
			MissingReason = reason;
		}
		public override string ToString() => Voucher?.ToString() ?? MissingReason.ToString();
	}

	public class MatchingResult
	{
		public List<InvoiceMatch> Matches { get; set; } = new List<InvoiceMatch>();
		public List<VoucherRecord> UnmatchedSLR { get; set; } = new List<VoucherRecord>();
		public List<VoucherRecord> UnmatchedLB { get; set; } = new List<VoucherRecord>();

		public IEnumerable<VoucherRecord> UnmatchedSLRShouldHaveOtherTrail
		{
			get
			{
				// SLR Voucher transactions with no accounts between 30000-80000, but with >80000 - these don't have any corresponding invoices
				Func<VoucherRecord, bool> is8plusOnly = voucher =>
					voucher.Transactions.Any(t => t.AccountId >= 80000)
					&& voucher.Transactions.Any(t => t.AccountId >= 30000 && t.AccountId < 80000) == false;

				return UnmatchedSLR.Where(o => is8plusOnly(o) == false);
			}
		}
	}

	public class InvoiceSIEMatch
	{
		public static MatchingResult MatchInvoiceWithLB_SLR(List<VoucherRecord> vouchers, List<InvoiceSummary> summaries, Dictionary<string, string>? alternativeCompanyNames = null)
		{
			var result = new MatchingResult();
			var matches = summaries.Select(o => new InvoiceMatch(o, new VoucherOrReason(VoucherMissingReason.Unknown), new VoucherOrReason(VoucherMissingReason.Unknown))).ToList();

			bool FuzzyMatchCompanyName(string invoiceSupplier, string voucherCompany) =>
				voucherCompany.Length > 25 && invoiceSupplier.Contains(voucherCompany)
				|| invoiceSupplier.EndsWith(voucherCompany) && voucherCompany.Length >= invoiceSupplier.Length * 0.4 && voucherCompany.Length > 6;

			bool MatchCompanyName(string invoiceSupplier, string voucherCompany) => invoiceSupplier == voucherCompany;
			bool MatchCompanyNameReplacements(string invoiceSupplier, string voucherCompany) => alternativeCompanyNames.GetValueOrDefault(invoiceSupplier, invoiceSupplier) == voucherCompany;

			Func<string, string, bool> matchCompanyName = alternativeCompanyNames == null ? MatchCompanyName : MatchCompanyNameReplacements;


			var searchMethods = new List<MatchDelegate> {
				(invoice, voucher) => LocalDate.FromDateTime(invoice.InvoiceDate.Value) == voucher.Date
					&& matchCompanyName(invoice.Supplier, voucher.CompanyName),

				(invoice, voucher) => Math.Abs(Period.Between(LocalDate.FromDateTime(invoice.InvoiceDate.Value), voucher.Date, PeriodUnits.Days).Days) <= 2
					&& FuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),
			};

			var matchSLRResult = Match(
				vouchers.Where(o => o.VoucherType == VoucherType.SLR),
				summaries,
				searchMethods,
				(invoice, voucher) => voucher.Transactions.Single(t => t.AccountId == 24400).Amount == -invoice.GrossAmount
			);

			//if (matchSLRResult.UnmatchedInvoices.Any())
			//{
			//	throw new Exception($"Unmatched invoices: {string.Join("\n", matchSLRResult.UnmatchedInvoices)}");
			//}
			// Set in result
			matchSLRResult.Matched.ForEach(o => matches.First(r => r.Invoice.Id == o.Item1.Id).SLR = new VoucherOrReason(o.Item2));
			result.UnmatchedSLR = matchSLRResult.UnmatchedVouchers;


			IEnumerable<LocalDate> GetProbableDates(InvoiceSummary invoice) => new[] { invoice.DueDate, invoice.FinalPostingingDate }.OfType<DateTime>().Select(LocalDate.FromDateTime);
			bool GetDateInRange(int days, int minInc, int maxEx) => days >= minInc && days < maxEx;
			// LB matches:
			var matchLBResult = Match(vouchers.Where(o => o.VoucherType == VoucherType.LB), summaries,
				new List<MatchDelegate> {
					(invoice, voucher) => GetProbableDates(invoice).Max() == voucher.Date
						&& matchCompanyName(invoice.Supplier, voucher.CompanyName),

					(invoice, voucher) => GetDateInRange(Period.Between(GetProbableDates(invoice).Min(), voucher.Date, PeriodUnits.Days).Days, 0, 20)
						&& FuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),

					//(invoice, voucher) => {
					//	//if (invoice.GrossAmount == 3938 && invoice.Supplier.StartsWith("Nyr")
					//	//	&& voucher.CompanyName.StartsWith("Nyr")) System.Diagnostics.Debugger.Break();
					//	return GetDateInRange(Period.Between(GetProbableDates(invoice).Min(), voucher.Date, PeriodUnits.Days).Days, 0, 20)
					//	&& fuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName); },

					(invoice, voucher) => GetDateInRange(Period.Between(GetProbableDates(invoice).Min(), voucher.Date, PeriodUnits.Days).Days, -1, 40)
						&& FuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),

					(invoice, voucher) => GetDateInRange(Period.Between(GetProbableDates(invoice).Max(), voucher.Date, PeriodUnits.Days).Days, 0, 40)
						&& FuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),
				},
				(invoice, voucher) => voucher.Transactions.Single(t => t.AccountId == 24400).Amount == invoice.GrossAmount
			);

			// Set in result
			matchLBResult.Matched.ForEach(o => matches.First(r => r.Invoice.Id == o.Item1.Id).LB = new VoucherOrReason(o.Item2));

			if (matchLBResult.UnmatchedInvoices.Any())
			{
				matchLBResult.UnmatchedInvoices.Where(o => o.FinalPostingingDate == null)
					.ToList()
					.ForEach(o => matches.First(r => r.Invoice.Id == o.Id).LB = new VoucherOrReason(VoucherMissingReason.InvoiceNotProcessed));


				var latestVoucherDate = vouchers.Max(o => o.Date).PlusDays(-3);
				matchLBResult.UnmatchedInvoices.Where(o => o.FinalPostingingDate != null)
					.Where(o => LocalDate.FromDateTime(o.DueDate.Value) > latestVoucherDate)
					.ToList()
					.ForEach(o => matches.First(r => r.Invoice.Id == o.Id).LB = new VoucherOrReason(VoucherMissingReason.DateRange));
			}
			if (matchLBResult.UnmatchedVouchers.Any())
			{
				var earliestInvoiceDueDate = summaries.Min(o => LocalDate.FromDateTime(o.DueDate.Value));
				var unexplained = matchLBResult.UnmatchedVouchers.Where(o => o.Date >= earliestInvoiceDueDate).ToList();
				result.UnmatchedLB = unexplained;
			}

			result.Matches = matches;
			return result;
		}

		public static MatchResult Match(IEnumerable<VoucherRecord> vouchers, IEnumerable<InvoiceSummary> invoices,
			List<MatchDelegate> searchPasses, MatchDelegate? commonSearch = null)
		{
			var matched = new List<(InvoiceSummary, VoucherRecord)>();

			var unmatchedVouchers = vouchers.ToList();
			var unmatchedInvoices = invoices.ToList();

			var searchPassIndex = -1;
			foreach (var searchPass in searchPasses)
			{
				searchPassIndex++;
				var matchAttempts = unmatchedInvoices.Select(invoice =>
				{
					var found = unmatchedVouchers.Where(voucher => searchPass(invoice, voucher));
					if (commonSearch != null)
						found = found.Where(voucher => commonSearch(invoice, voucher));
					return new { Invoice = invoice, Vouchers = found.ToList() };
				}).ToList();

				var newMatched = new List<(InvoiceSummary, VoucherRecord)>();

				// Handle matches:
				matchAttempts.Where(o => o.Vouchers.Count == 1).ToList().ForEach(o =>
				{
					newMatched.Add((o.Invoice, o.Vouchers.First()));
				});

				// Find if we have "identical" invoices/vouchers
				var grouped = matchAttempts.Where(o => o.Vouchers.Count > 1).GroupBy(o => o.Invoice.ToString()).Where(o => o.Count() > 1);
				if (grouped.Any())
				{
					foreach (var grp in grouped)
					{
						var numVouchers = grp.Select(o => o.Vouchers.Count).Distinct().SingleOrDefault();
						if (numVouchers == grp.Count())
						{
							var commonVouchers = grp.First().Vouchers;
							var commonInvoices = grp.Select(o => o.Invoice).ToList();
							for (int i = 0; i < commonVouchers.Count; i++)
								newMatched.Add((commonInvoices[i], commonVouchers[i]));
						}
					}
				}
				newMatched.ForEach(o =>
				{
					unmatchedVouchers.RemoveAll(vou => vou.Id == o.Item2.Id);
					unmatchedInvoices.RemoveAll(inv => inv.Id == o.Item1.Id);
				});
				matched.AddRange(newMatched);
			}

			return new MatchResult { Matched = matched, UnmatchedInvoices = unmatchedInvoices, UnmatchedVouchers = unmatchedVouchers };
		}
	}

	public delegate bool MatchDelegate(InvoiceSummary invoice, VoucherRecord voucher);

	public class MatchResult
	{
		public List<(InvoiceSummary, VoucherRecord)> Matched { get; set; } = new List<(InvoiceSummary, VoucherRecord)>();
		public List<InvoiceSummary> UnmatchedInvoices { get; set; } = new List<InvoiceSummary>();
		public List<VoucherRecord> UnmatchedVouchers { get; set; } = new List<VoucherRecord>();
	}
}
