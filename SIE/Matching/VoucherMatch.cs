using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIE.Matching
{
	public class MatchSLRResult
	{
		//public List<(VoucherRecord slr, VoucherRecord other)> Matches { get; set; } = new List<(VoucherRecord slr, VoucherRecord other)>();
		public List<Matched> Matches { get; set; } = new List<Matched>();
		/// <summary>
		/// Multiple matches found - the first one was added to Matched list
		/// </summary>
		public List<string> Ambiguous { get; set; } = new List<string>();
		public List<VoucherRecord> NotMatchedSLR { get; set; } = new List<VoucherRecord>();
		public List<VoucherRecord> NotMatchedOther { get; set; } = new List<VoucherRecord>();

		public class Matched
		{
			public VoucherRecord SLR { get; set; } = new VoucherRecord();
			public VoucherRecord Other { get; set; } = new VoucherRecord();
			public int AccountIdNonAdmin { get => SLR.Transactions.FirstOrDefault(o => !(new[] { '1', '2' }.Contains(o.AccountId.ToString()[0])))?.AccountId ?? 0; }
			public string CompanyName { get => SLR.CompanyName; }
		}

		public static MatchSLRResult MatchSLRVouchers(IEnumerable<VoucherRecord> vouchers, IEnumerable<VoucherType>? filterVoucherTypes = null)
		{
			if (filterVoucherTypes != null)
				vouchers = vouchers.Where(vr => !filterVoucherTypes.Contains(vr.VoucherType));

			var result = new MatchSLRResult();
			VoucherRecord.NormalizeCompanyNames(vouchers);

			//All transactions within a voucher seem to reference the same company (so Transactions.First() is OK):
			var groupedByCompany = vouchers.Where(o => o.Transactions.Any()).GroupBy(o => o.Transactions.First().CompanyName).ToDictionary(o => o.Key, o => o.ToList());

			var slrTypes = new[] { VoucherType.SLR, VoucherType.TaxAndExpense, VoucherType.KR }.ToList();
			//TODO: Found KR / KB matches - seem to be mostly personal expenses, what do they stand for?
			foreach (var kv in groupedByCompany)
			{
				var slrs = kv.Value.Where(o => slrTypes.Contains(o.VoucherType)).OrderBy(o => o.Date).ToList();
				var byAmount = kv.Value.Except(slrs).GroupBy(o => FindRelevantAmount(o.Transactions)).ToDictionary(o => o.Key, o => o.ToList());

				//			var byAmountX = kv.Value.Except(slrs).SelectMany(o => o.TransactionsNonAdminOrCorrections.Select(p => new { Voucher = o, Transaction = p }))
				//.GroupBy(o => o.Transaction.Amount).ToDictionary(o => o.Key, o => o.ToList());

				//Try to find matching Company and Amount
				//Done in passes - first match those close in time, then look later. We have found examples of more than 9 months diff
				var maxDaysBetweenIterations = new[] { 0, 20, 35, 70, 100, 300 };
				foreach (var maxDays in maxDaysBetweenIterations)
				{
					var slrToRemove = new List<VoucherRecord>();
					foreach (var slr in slrs)
					{
						//TODO: there can be multiple separate accounts involved - can't look for just one amount!
						// It's actually transaction <-> transaction match we're after, not Voucher <-> Voucher
						var amount = FindRelevantAmount(slr.Transactions);
						if (byAmount.TryGetValue(amount, out var list))
						{
							var hits = list.Where(o => o.Date >= slr.Date && Period.Between(slr.Date, o.Date, PeriodUnits.Days).Days <= maxDays)
								.OrderBy(o => o.Date).ToList();
							if (!hits.Any())
								continue;
							if (hits.Count > 1)
							{
								result.Ambiguous.Add(slr.ToHierarchicalString() + " " + string.Join("\n", hits.Select(o => o.ToHierarchicalString())));
								hits = hits.Take(1).ToList();
							}
							var hit = hits.Single();
							list.Remove(hit);
							if (!list.Any())
								byAmount.Remove(amount);
							result.Matches.Add(new MatchSLRResult.Matched { SLR = slr, Other = hit });
							slrToRemove.Add(slr);
						}
					}
					slrs = slrs.Except(slrToRemove).ToList();
				}
				result.NotMatchedSLR.AddRange(slrs);
			}

			result.NotMatchedOther = vouchers.Where(o => !slrTypes.Contains(o.VoucherType)).Except(result.Matches.Select(o => o.Other)).ToList();


			//Special handling: of LR vs LB - expenses don't have proper CompanyName. LB will have person name, LR will have description of expense
			var expenses = result.NotMatchedSLR.Where(o => o.VoucherType == VoucherType.TaxAndExpense);
			var lbsByAmount = result.NotMatchedOther.Where(o => o.VoucherType == VoucherType.LB).GroupBy(o => FindRelevantAmount(o.Transactions)).ToDictionary(o => o.Key, o => o.ToList());
			var maxNumDaysForProcessingExpense = 5;
			var additionalMatches = result.Matches.Take(0).ToList();
			foreach (var item in expenses)
			{
				var amount = FindRelevantAmount(item.Transactions);
				if (lbsByAmount.TryGetValue(amount, out var list))
				{
					var latestDate = item.Date.PlusDays(maxNumDaysForProcessingExpense);
					var found = list.Where(o => o.Date >= item.Date && o.Date <= latestDate);
					if (found.Count() == 1)
					{
						additionalMatches.Add(new MatchSLRResult.Matched { SLR = item, Other = found.Single() });
						list.Remove(found.Single());
					}
				}
			}

			//Add found to matches, remove from others:
			result.Matches.AddRange(additionalMatches);
			result.NotMatchedOther = result.NotMatchedOther.Except(additionalMatches.Select(o => o.Other)).ToList();
			result.NotMatchedSLR = result.NotMatchedSLR.Except(additionalMatches.Select(o => o.SLR)).ToList();

			return result;

			static decimal FindRelevantAmount(IEnumerable<TransactionRecord> txs) => txs.Select(o => Math.Abs(o.Amount)).Max();
		}
	}
}
