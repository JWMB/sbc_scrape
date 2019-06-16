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
		public string Evaluate(List<InvoiceSummary> invoices, List<Receipt> receipts, List<BankTransaction> transactions)
		{
			var dateRange = (Min: new DateTime(2019, 1, 1), Max: DateTime.Today);
			Func<DateTime, bool> inRange = d => d >= dateRange.Min && d.Date <= dateRange.Max;
			//Func<InvoiceSummary, DateTime?> GetRelevantDate = o => o.FinalPostingingDate.HasValue && o.FinalPostingingDate.Value > o.DueDate.Value ? o.FinalPostingingDate.Value : o.DueDate.Value;

			invoices = invoices.Where(o => inRange(o.DueDate.Value)).ToList();
			receipts = receipts.Where(o => inRange(o.Date)).ToList();
			transactions = transactions?.Where(o => inRange(o.AccountingDate)).ToList();

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
			var compTransact = transactions.Select(o => new Comparable {
				Date = o.AccountingDate,
				OptionalDate = o.CurrencyDate,
				Amount = -o.Amount });
			//HS 15069192 = Handelsbanken
			//LB UTTAG = invoice?

			var passes = new Comparable.EqualityComparer[] {
				new Comparable.EqualityComparer(1),
				new Comparable.EqualityComparer(2),
				new Comparable.EqualityComparer(6, true),
				new Comparable.EqualityComparer(2, false, new Dictionary<string, string>{ { "SBC SVERIGES BOSTADSRÄTTSCENTRUM AB", "SBC Sv Bostadsrättscentrum" } }),
			};

			var allJoined = new List<InvoiceAndOrReceipt> ();
			foreach (var comparer in passes)
			{
				var joined = compInvoices.Join(compReceipts, i => i, r => r, (i, r) =>
					new InvoiceAndOrReceipt(i.Object as InvoiceSummary, r.Object as Receipt), comparer);

				allJoined.AddRange(joined);

				var joinedInvoices = joined.Select(o => o.Invoice).ToList();
				compInvoices = compInvoices.Where(o => !joinedInvoices.Contains(o.Object));
				var joinedReceipts = joined.Select(o => o.Receipt).ToList();
				compReceipts = compReceipts.Where(o => !joinedReceipts.Contains(o.Object));
			}

			//Includes both matched and unmatched
			var invoicesAndOrReceipts = allJoined
				.Concat(receipts.Except(allJoined.Select(o => o.Receipt)).Select(o => new InvoiceAndOrReceipt(null, o)))
				.Concat(invoices.Except(allJoined.Select(o => o.Invoice)).Select(o => new InvoiceAndOrReceipt(o, null)))
				.ToList();

			//6091 LB32 (LB UTTAG): sum of receipts for that date
			//6091 BGINB: incoming (will not be matched)
			//6091 AG: ?

			var searchTransactions = new List<BankTransaction>(transactions);
			var searchInvRecs = new List<InvoiceAndOrReceipt>(invoicesAndOrReceipts);
			var allMatchedTransactions = new List<MatchedTransaction>();

			void PerformSearch(Func<List<BankTransaction>, Dictionary<DateTime, List<InvoiceAndOrReceipt>>, IEnumerable<MatchedTransaction>> f)
			{
				var invrecByDateX = searchInvRecs.GroupBy(o => o.Date).ToDictionary(g => g.Key, g => g.ToList());
				var matchedTransactionsX = f(searchTransactions, invrecByDateX).Where(o => o != null);
				allMatchedTransactions.AddRange(matchedTransactionsX);

				searchTransactions = searchTransactions.Except(matchedTransactionsX.Select(o => o.Transaction)).ToList();
				searchInvRecs = searchInvRecs.Except(matchedTransactionsX.SelectMany(o => o.InvRecs)).ToList();
			}

			//Match LB32 transactions with receipts/invoices for that date:
			PerformSearch((searchTx, invrecByDateX) =>
				searchTx.Where(o => o.Reference == "6091 LB32").Select(tx => {
					if (invrecByDateX.TryGetValue(tx.AccountingDate, out var invrecsForDate)) {
						var sumDiff = invrecsForDate.Sum(o => o.Amount) + tx.Amount; //tx.Amount is negative
						if (sumDiff == 0)
							return new MatchedTransaction { Transaction = tx, InvRecs = invrecsForDate };
						else if (sumDiff > 0) {
							//If we remove those with the exact diffing amount, is that just a single one?
							var found = invrecsForDate.Where(o => o.Amount != sumDiff);
							if (found.Count() == invrecsForDate.Count - 1)
								return new MatchedTransaction { Transaction = tx, InvRecs = found.ToList() };
						}
					}
					return null;
				})
			);

			//SKATTEVERKET are always separate transactions
			PerformSearch((searchTx, invrecByDateX) => 
				searchTx.Where(o => o.Reference == "6091 LB32").Select(tx => {
					if (invrecByDateX.TryGetValue(tx.AccountingDate, out var invrecsForDate)) {
						var found = invrecsForDate.Where(o => o.Supplier == "SKATTEVERKET");
						if (found.Any() && found.Sum(o => o.Amount) == -tx.Amount)
							return new MatchedTransaction { Transaction = tx, InvRecs = found.ToList() };
					}
					return null;
				})
			);

			//6044 SHYP (HS 15069192): interest to bank(?)
			PerformSearch((searchTx, invrecByDateX) =>
				searchTx.Where(o => o.Reference == "6044 SHYP").Select(tx => {
					if (invrecByDateX.TryGetValue(tx.AccountingDate, out var invrecsForDate)) {
						var found = invrecsForDate.Where(o => o.Supplier == "Handelsbanken");
						if (found.Any() && found.Sum(o => o.Amount) == -tx.Amount)
							return new MatchedTransaction { Transaction = tx, InvRecs = found.ToList() };
					}
					return null;
				})
			);
			PerformSearch((searchTx, invrecByDateX) =>
				searchTx.Where(o => o.Reference == "6044 SHYP").Select(tx => {
					var found = searchInvRecs.Where(o => o.Supplier == "Handelsbanken" && Math.Abs((tx.AccountingDate - o.Date).TotalDays) <= 2);
					if (found.Any()) {
						if (found.Sum(o => o.Amount) == -tx.Amount)
							return new MatchedTransaction { Transaction = tx, InvRecs = found.ToList() };
						else if (found.Where(o => o.Amount == -tx.Amount).Count() == 1)
							return new MatchedTransaction { Transaction = tx, InvRecs = found.Where(o => o.Amount == -tx.Amount).ToList() };
					}
					return null;
				})
			);

			//Expensive permutations last (with less data)
			PerformSearch((searchTx, invrecByDateX) =>
				searchTx.Where(o => o.Reference == "6091 LB32").Select(tx => {
					if (invrecByDateX.TryGetValue(tx.AccountingDate, out var invrecsForDate)) {
						//Remove (some) combination of invoices/receipts to see if we get a match
						for (int numToRemove = 2; numToRemove < Math.Min(4, invrecsForDate.Count); numToRemove++)
							foreach (var list in GenerateCombos(invrecsForDate, numToRemove)) {
								var tmp = invrecsForDate.Except(list);
								if (tmp.Sum(o => o.Amount) == -tx.Amount)
									return new MatchedTransaction { Transaction = tx, InvRecs = tmp.ToList() };
							}
					}
					return null;
				})
			);


			var sorted = searchInvRecs.Select(o => new {
				o.Date, Output = $"{o.Amount}\t{o.Supplier}\t{o.ContainsCode}" })
				.Concat(searchTransactions.Where(o => o.Reference != "6091 BGINB").Select(o => new {
					Date = o.AccountingDate, Output = $"{o.Amount}\t{o.Text}\t{o.Reference}"}))
				.OrderByDescending(o => o.Date)
				.Select(o => $"{o.Date.ToShortDateString()}\t{o.Output}").ToList();

			//var sorted = allJoined.Select(o => new {
			//	o.Receipt.Date, Output = $"{o.Invoice.GrossAmount}\t{o.Invoice.Supplier}\t{o.Invoice.Id}"})
			//	.Concat(transactions.Select(o => new {
			//		Date = o.AccountingDate, Output = $"{o.Amount}\t{o.Text}\t{o.Reference}"
			//	}))
			//	.OrderByDescending(o => o.Date)
			//	.Select(o => $"{o.Date.ToShortDateString()}\t{o.Output}").ToList();

			//var sorted = SortRows(unhandledInvoices, unhandledReceipts, null);
			return string.Join("\n", sorted);
		}

		List<string> SortRows(IEnumerable<InvoiceSummary> invoices, IEnumerable<Receipt> receipts, IEnumerable<BankTransaction> transactions)
		{
			invoices = invoices ?? new InvoiceSummary[] { };
			receipts = receipts ?? new Receipt[] { };
			transactions = transactions ?? new BankTransaction[] { };

			return invoices.Where(o => o.DueDate.HasValue).Select(o =>
				new { Date = o.DueDate.Value, Output = $"{o.GrossAmount}\t{o.Supplier}\t{o.SupplierId}\t{o.Id}\t{o.FinalPostingingDate}" })
				.Concat(receipts.Select(o => new { o.Date, Output = $"{o.Amount}\t{o.Supplier}\t{o.SupplierId}" }))
				.Concat(transactions.Where(o => !(o.Text.Contains("56901309") && o.Amount > 0)).Select(o =>
					new {
						Date = o.AccountingDate,
						Output = $"{-o.Amount}\t{(o.Text == "HS 15069192" ? "Handelsbanken!" : o.Text)}\t{o.CurrencyDate}"
					}))
				.OrderByDescending(o => o.Date)
				.Select(o => $"{o.Date.ToShortDateString()}\t{o.Output}").ToList();
		}

		public static IEnumerable<List<T>> GenerateCombos<T>(List<T> items, int numItems)
		{
			var indices = new List<int>();
			for (int i = 0; i < numItems; i++)
				indices.Add(i);
			var lastIndex = numItems - 1;
			while (true)
			{
				yield return indices.Select(i => items[i]).ToList();
				if (indices[lastIndex] < items.Count - 1)
					indices[lastIndex]++;
				else
				{
					var indexToMove = lastIndex;
					while (true) //Find which index to reset to:
					{
						indexToMove--;
						var start = indices[indexToMove];
						if (start < indices[indexToMove + 1] - 1)
							break;
					}
					var startX = indices[indexToMove];
					for (int i = indexToMove; i < numItems; i++)
						indices[i] = startX + i - indexToMove + 1;
					if (indices[0] == items.Count - numItems)
					{
						yield return indices.Select(i => items[i]).ToList();
						break;
					}
				}
			}
		}

		class InvoiceAndOrReceipt
		{
			public Receipt Receipt;
			public InvoiceSummary Invoice;
			public DateTime Date => Receipt?.Date ?? Invoice.DueDate.Value;
			public decimal Amount => Receipt?.Amount ?? Invoice.GrossAmount;
			public string Supplier => Invoice?.Supplier ?? Receipt?.Supplier;
			public string ContainsCode => (Invoice == null ? "" : "INV") + (Receipt == null ? "" : "REC");

			public InvoiceAndOrReceipt(InvoiceSummary invoice, Receipt receipt)
			{
				Invoice = invoice;
				Receipt = receipt;
			}
		}
		class MatchedTransaction
		{
			public BankTransaction Transaction;
			public List<InvoiceAndOrReceipt> InvRecs;
		}

		class Comparable
		{
			public DateTime Date;
			public DateTime OptionalDate;
			public decimal Amount;
			public string Supplier;
			public string SupplierId;
			public object Object;

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
