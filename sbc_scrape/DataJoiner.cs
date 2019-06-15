using MediusFlowAPI;
using sbc_scrape.SBC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sbc_scrape
{
	class DataJoiner
	{
		public string Evaluate(List<InvoiceSummary> invoices, List<Receipt> receipts)
		{
			var dateRange = (Min: new DateTime(2019, 1, 1), Max: DateTime.Today);
			Func<DateTime, bool> inRange = d => d >= dateRange.Min && d.Date <= dateRange.Max;
			//Func<InvoiceSummary, DateTime?> GetRelevantDate = o => o.FinalPostingingDate.HasValue && o.FinalPostingingDate.Value > o.DueDate.Value ? o.FinalPostingingDate.Value : o.DueDate.Value;

			invoices = invoices.Where(o => inRange(o.DueDate.Value)).ToList();
			receipts = receipts.Where(o => inRange(o.Date)).ToList();

			var compInvoices = invoices.Select(o => new Comparable {
				Date = o.DueDate.Value,
				OptionalDate = o.FinalPostingingDate ?? DateTime.MinValue,
				Amount = o.GrossAmount,
				SupplierId = o.SupplierId,
				Supplier = o.Supplier,
				Object = o });
			var compReceipts = receipts.Select(o => new Comparable {
				Date = o.Date,
				OptionalDate = o.Date,
				Amount = o.Amount,
				SupplierId = o.SupplierId,
				Supplier = o.Supplier,
				Object = o });
			//var compTransact = transactions.Select(o => new Comparable { Date = o.AccountingDate, Amount = -o.Amount });
			//HS 15069192 = Handelsbanken
			//LB UTTAG = invoice

			var intersect = compInvoices.Intersect(compReceipts, new Comparable.EqualityComparer(1)).ToList();

			var passes = new Comparable.EqualityComparer[] {
				new Comparable.EqualityComparer(1),
				new Comparable.EqualityComparer(2),
				new Comparable.EqualityComparer(6, true),
				new Comparable.EqualityComparer(2, false, new Dictionary<string, string>{ { "SBC SVERIGES BOSTADSRÄTTSCENTRUM AB", "SBC Sv Bostadsrättscentrum" } }),
			};

			var allJoined = new List<(InvoiceSummary Invoice, Receipt Receipt)> ();
			foreach (var comparer in passes)
			{
				var joined = compInvoices.Join(compReceipts, i => i, r => r, (i, r) => (
					Invoice: i.Object as InvoiceSummary,
					Receipt: r.Object as Receipt
				), comparer);

				allJoined.AddRange(joined);

				var joinedInvoices = joined.Select(o => o.Invoice).ToList();
				compInvoices = compInvoices.Where(o => !joinedInvoices.Contains(o.Object));
				var joinedReceipts = joined.Select(o => o.Receipt).ToList();
				compReceipts = compReceipts.Where(o => !joinedReceipts.Contains(o.Object));
			}

			var unhandledInvoices = invoices.Except(allJoined.Select(o => o.Invoice));
			var unhandledReceipt = receipts.Except(allJoined.Select(o => o.Receipt));

			var sorted = SortRows(unhandledInvoices, unhandledReceipt);
			//var sorted = SortRows(invoices, receipts);
			return string.Join("\n", sorted);
		}

		List<string> SortRows(IEnumerable<InvoiceSummary> invoices, IEnumerable<Receipt> receipts)
		{
			return invoices.Where(o => o.DueDate.HasValue).Select(o =>
				new { Date = o.DueDate.Value, Output = $"{o.GrossAmount}\t{o.Supplier}\t{o.SupplierId}\t{o.Id}\t{o.FinalPostingingDate}" })
				.Concat(
				receipts.Select(o => new { o.Date, Output = $"{o.Amount}\t{o.Supplier}\t{o.SupplierId}" })
				//transactions.Where(o => !(o.Text.Contains("56901309") && o.Amount > 0)).Select(o => //Ignore 
				//	new {
				//		Date = o.AccountingDate,
				//		Output = $"{-o.Amount}\t{(o.Text == "HS 15069192" ? "Handelsbanken!" : o.Text)}\t{o.CurrencyDate}"
				//	}))
				).OrderByDescending(o => o.Date)
				.Select(o => $"{o.Date.ToShortDateString()}\t{o.Output}").ToList();
		}

		class Comparable
		{
			public DateTime Date;
			public DateTime OptionalDate;
			public decimal Amount;
			public string Supplier;
			public string SupplierId;
			public object Object;

			//public override int GetHashCode()
			//{
			//	return Amount.GetHashCode();
			//}
			//public override bool Equals(object obj)
			//{
			//	if (!(obj is Comparable other))
			//		return false;
			//	return other.SupplierId == SupplierId && other.Amount == Amount && Math.Abs((other.Date - Date).TotalDays) <= 2;
			//}

			public class EqualityComparer : IEqualityComparer<Comparable>
			{
				private readonly int maxDays;
				private readonly bool useOptionalDate;
				private readonly Dictionary<string, string> supplierReplacements;

				public EqualityComparer(int maxDays = 0, bool useOptionalDate = false, Dictionary<string, string> supplierReplacements = null)
				{
					this.maxDays = maxDays;
					this.useOptionalDate = useOptionalDate;
					this.supplierReplacements = supplierReplacements;
				}

				public bool Equals(Comparable b1, Comparable b2)
				{
					if (b2 == null && b1 == null) return true;
					else if (b1 == null || b2 == null) return false;
					else return
						b1.Amount == b2.Amount
						&& (useOptionalDate
							? Math.Abs((b1.OptionalDate - b2.OptionalDate).TotalDays) <= maxDays
							: Math.Abs((b1.Date - b2.Date).TotalDays) <= maxDays)
						&& (supplierReplacements == null
							? b1.SupplierId == b2.SupplierId
							: supplierReplacements.GetValueOrDefault(b1.Supplier, b1.Supplier) == supplierReplacements.GetValueOrDefault(b2.Supplier, b2.Supplier));
				}
				public int GetHashCode(Comparable o) => o.Amount.GetHashCode();
			}
		}


		static class LevenshteinDistance
		{
			public static int Compute(string s, string t)
			{
				var n = s.Length;
				var m = t.Length;
				var d = new int[n + 1, m + 1];

				if (n == 0)
					return m;
				if (m == 0)
					return n;

				for (var i = 0; i <= n; d[i, 0] = i++) { }
				for (var j = 0; j <= m; d[0, j] = j++) { }

				for (var i = 1; i <= n; i++) {
					for (var j = 1; j <= m; j++) {
						var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

						d[i, j] = Math.Min(
							Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
							d[i - 1, j - 1] + cost);
					}
				}
				return d[n, m];
			}
		}
	}
}
