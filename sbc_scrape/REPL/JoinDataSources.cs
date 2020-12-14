using REPL;
using sbc_scrape.SBC;
using SBCScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
	class JoinDataSources : Command
	{
		private readonly string defaultFolder;
		private readonly Main main;

		public JoinDataSources(string defaultFolder, Main main)
		{
			this.defaultFolder = defaultFolder;
			this.main = main;
		}
		public override string Id => "join";
		public override async Task<object> Evaluate(List<object> parms)
		{
			Console.WriteLine("0");
			var invoices = (await main.LoadInvoices(includeOCRd: false, (i, l) => {
				if (l < 100 || i % 20 == 0) Console.RewriteLine($"{i}/{l}");
			})).Where(o => o.DueDate.HasValue).ToList();
			//var invoices = (await main.LoadInvoices(false)).Where(o => o.DueDate.HasValue).ToList();
			var receipts = new ReceiptsSource().ReadAll(defaultFolder);
			var transactions = new BankTransactionSource().ReadAll(defaultFolder);

			//TODO: parms date from
			var dateRange = (Min: new DateTime(2016, 1, 1), Max: DateTime.Today);
			bool inRange(DateTime d) => d >= dateRange.Min && d.Date <= dateRange.Max;

			invoices = invoices.Where(o => inRange(o.DueDate.Value)).ToList();
			receipts = receipts.Where(o => inRange(o.Date)).ToList();
			transactions = transactions?.Where(o => inRange(o.AccountingDate)).ToList();

			var trxOrderWithinDate = new Dictionary<DateTime, List<decimal>>();
			foreach (var o in transactions)
			{
				if (!trxOrderWithinDate.TryGetValue(o.CurrencyDate, out var list))
				{
					list = new List<decimal>();
					trxOrderWithinDate.Add(o.CurrencyDate, list);
				}
				list.Add(o.TotalAccountAmount);
			}

			var joined = new sbc_scrape.DataJoiner().Join(invoices, receipts, transactions, out var unmatched);

			var sortedUnmatched = unmatched.invoicesAndReceipts.Select(o => new {
				o.Date,
				Output = $"{o.Amount}\t{o.Supplier}\t{o.ContainsCode}"
			})
				.Concat(unmatched.transactions.Where(o => o.Reference != "6091 BGINB").Select(o => new {
					Date = o.AccountingDate,
					Output = $"{o.Amount}\t{o.Text}\t{o.Reference}"
				}))
				.OrderByDescending(o => o.Date)
				.Select(o => $"{o.Date.ToShortDateString()}\t{o.Output}").ToList();

			var dbg = string.Join("\n", sortedUnmatched);

			static decimal AmountCorrectSign(decimal amount, BankTransaction trx)
			{
				return amount * ((trx != null &&
					(trx.Reference.EndsWith(" BGINB")
					|| trx.Reference.EndsWith(" AG")
					|| trx.Reference.EndsWith(" AVGIFT"))) ? 1 : -1); //|| trx.Reference.EndsWith("LÖN UTTAG")
			}

			var byInvRec = joined.SelectMany(o => o.InvRecs.Select(i => new JoinedRow
			{
				Date = i.Date,
				Amount = AmountCorrectSign(i.Amount, o.Transaction),
				Supplier = i.Supplier,
				AccountId = i.Invoice?.AccountId,
				AccountName = i.Invoice?.AccountName,
				Comments = i.Invoice?.Comments ?? i.Receipt?.OCR,
				InvoiceId = i.Invoice?.Id,
				ReceiptId = i.Receipt?.Information,
				CurrencyDate = o.Transaction.CurrencyDate,
				TransactionText = o.Transaction.Text,
				TransactionRef = o.Transaction.Reference,
				//AmountAccFromTrx = o.Transaction.TotalAccountAmount,
				//OrderWithinDate = trxOrderWithinDate[o.Transaction.CurrencyDate].IndexOf(o.Transaction.TotalAccountAmount),
			}))
				.Concat(unmatched.invoicesAndReceipts.Select(o => new JoinedRow
				{
					Date = o.Date,
					Missing = "TRX",
					Amount = o.Amount,
					Supplier = o.Supplier,
					AccountId = o.Invoice?.AccountId,
					AccountName = o.Invoice?.AccountName,
					InvoiceId = o.Invoice?.Id,
					Comments = o.Invoice?.Comments,
					ReceiptId = o.Receipt?.Information,
					CurrencyDate = null,
					TransactionText = null,
					TransactionRef = null,
					//OrderWithinDate = 0,
				})
				.Concat(unmatched.transactions.Select(o => new JoinedRow
				{
					Date = o.AccountingDate,
					Missing = o.Amount > 0 ? "" : "I/R", //When incoming (amount > 0), there is no invoice/receipt
					Amount = o.Amount,
					Supplier = null,
					InvoiceId = null,
					ReceiptId = null,
					CurrencyDate = o.CurrencyDate,
					TransactionText = o.Text,
					TransactionRef = o.Reference,
					//AmountAccFromTrx = o.TotalAccountAmount,
					//OrderWithinDate = trxOrderWithinDate[o.CurrencyDate].IndexOf(o.TotalAccountAmount),
				}))
				).OrderByDescending(o => o.CurrencyDate ?? o.Date).ToList(); //.ThenBy(o => o.OrderWithinDate).ToList();

			//adjust incoming amountAcc to reflect that transactions include the total sum of the first transactions of first date in start value
			//(which may reflect multiple invoices/receipts)
			//var amountAcc = transactions.Last().TotalAccountAmount - transactions.Last().Amount;
			//for (int i = byInvRec.Count - 1; i >= 0; i--)
			//{
			//	var item = byInvRec[i];
			//	amountAcc += string.IsNullOrEmpty(item.TransactionRef) ? 0 : item.Amount;
			//	item.AmountAcc = amountAcc;
			//}
			var rxReplace = new System.Text.RegularExpressions.Regex(@"\s\(\d*\)");
			foreach (var row in byInvRec)
				if (!string.IsNullOrEmpty(row.Comments))
					row.Comments = rxReplace.Replace(row.Comments, "");
			return byInvRec;
		}

		class JoinedRow
		{
			public DateTime Date { get; set; }
			public string Missing { get; set; }
			public decimal Amount { get; set; }
			public string Supplier { get; set; }
			public long? AccountId { get; set; }
			public string AccountName { get; set; }
			public string Comments { get; set; }
			public long? InvoiceId { get; set; }
			public string ReceiptId { get; set; }
			public DateTime? CurrencyDate { get; set; }
			public string TransactionText { get; set; }
			public string TransactionRef { get; set; }
			//public decimal AmountAcc { get; set; }
			//public decimal AmountAccFromTrx { get; set; }
			//public int OrderWithinDate { get; set; }
		}
	}
}
