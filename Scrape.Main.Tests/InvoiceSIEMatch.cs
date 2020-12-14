using MediusFlowAPI;
using NodaTime;
using SIE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scrape.Main.Tests
{
	//public struct Match
	//{
	//	public InvoiceSummary Invoice { get; private set; }
	//	public VoucherRecord LB { get; private set; }
	//	public VoucherRecord SLR { get; private set; }

	//	public Match()
	//	{
	//	}
	//}

	public class InvoiceSIEMatch
	{
		public static void XX(List<VoucherRecord> vouchers, List<InvoiceSummary> summaries)
		{
			Func<string, string, bool> fuzzyMatchCompanyName = (invoiceSupplier, voucherCompany) =>
invoiceSupplier.EndsWith(voucherCompany) && voucherCompany.Length >= invoiceSupplier.Length * 0.4 && voucherCompany.Length > 6;

			var matchSLRResult = Match(
				vouchers.Where(o => o.VoucherType == VoucherType.SLR),
				summaries,
				new List<MatchDelegate> {
					(invoice, voucher) => LocalDate.FromDateTime(invoice.InvoiceDate.Value) == voucher.Date
						&& invoice.Supplier == voucher.CompanyName,

					(invoice, voucher) => Math.Abs(Period.Between(LocalDate.FromDateTime(invoice.InvoiceDate.Value), voucher.Date, PeriodUnits.Days).Days) <= 2
						&& fuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),
				},
				(invoice, voucher) => voucher.Transactions.Single(t => t.AccountId == 24400).Amount == -invoice.GrossAmount
			);

			if (matchSLRResult.UnmatchedInvoices.Any())
			{
				throw new Exception($"Unmatched invoices: {string.Join("\n", matchSLRResult.UnmatchedInvoices)}");
			}
			// Note: SLR Voucher transactions with no accounts between 30000-80000, but with >80000 - these don't have any corresponding invoices
			Func<VoucherRecord, bool> is8plusOnly = voucher =>
				voucher.Transactions.Any(t => t.AccountId >= 80000)
				&& voucher.Transactions.Any(t => t.AccountId >= 30000 && t.AccountId < 80000) == false;

			var shouldAppearInReceiptsOrOtherSBCSource = matchSLRResult.UnmatchedVouchers.Where(o => is8plusOnly(o) == false).ToList();

			// Only unmatched SLRs in vouchers after this:
			vouchers = vouchers.Where(o => o.VoucherType != VoucherType.SLR).Concat(matchSLRResult.UnmatchedVouchers).ToList();


			Func<InvoiceSummary, IEnumerable<LocalDate>> getProbableDates = invoice => new[] { invoice.DueDate, invoice.FinalPostingingDate }.OfType<DateTime>().Select(LocalDate.FromDateTime);
			// LB matches:
			var matchLBResult = Match(vouchers.Where(o => o.VoucherType == VoucherType.LB), summaries,
				new List<MatchDelegate> {
					(invoice, voucher) => getProbableDates(invoice).Max() == voucher.Date
						&& invoice.Supplier == voucher.CompanyName,

					(invoice, voucher) => Period.Between(getProbableDates(invoice).Min(), voucher.Date, PeriodUnits.Days).Days <= 20
						&& fuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),

					(invoice, voucher) => Period.Between(getProbableDates(invoice).Min(), voucher.Date, PeriodUnits.Days).Days <= 40
						&& fuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),

					(invoice, voucher) => Period.Between(getProbableDates(invoice).Max(), voucher.Date, PeriodUnits.Days).Days <= 40
						&& fuzzyMatchCompanyName(invoice.Supplier, voucher.CompanyName),
				},
				(invoice, voucher) => voucher.Transactions.Single(t => t.AccountId == 24400).Amount == invoice.GrossAmount
			);
			if (matchLBResult.UnmatchedInvoices.Any())
			{
				var latestVoucherDate = vouchers.Max(o => o.Date).PlusDays(-3);
				var unexplained = matchLBResult.UnmatchedInvoices.Where(o => LocalDate.FromDateTime(o.DueDate.Value) < latestVoucherDate).ToList();
				if (unexplained.Any())
				{ }
			}
			if (matchLBResult.UnmatchedVouchers.Any())
			{
				var earliestInvoiceDueDate = summaries.Min(o => LocalDate.FromDateTime(o.DueDate.Value));
				var unexplained = matchLBResult.UnmatchedVouchers.Where(o => o.Date >= earliestInvoiceDueDate).ToList();
				if (unexplained.Any())
				{ }
			}
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
