using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SIE
{
	public class VoucherType
	{
		public string Code { get; private set; }
		public string Name { get; private set; }

		private VoucherType(string code, string name)
		{
			Code = code;
			Name = name;
		}
		public override string ToString()
		{
			return $"{Code} ({Name})";
		}

		private static Dictionary<string, VoucherType> _lookup = new Dictionary<string, VoucherType>();
		private static Dictionary<string, VoucherType> Lookup
		{
			get
			{
				if (!_lookup.Any())
				{
					_lookup = new[] {
							new VoucherType("AR", nameof(AR)),
							new VoucherType("AV", nameof(AV)),
							new VoucherType("BS", nameof(BS)),
							new VoucherType("CR", nameof(CR)),
							new VoucherType("FAS", nameof(FAS)),
							new VoucherType("KB", nameof(KB)),
							new VoucherType("KR", nameof(KR)),
							new VoucherType("SLR", nameof(SLR)),
							new VoucherType("LON", nameof(Salary)),
							new VoucherType("LAN", nameof(LAN)),
							new VoucherType("LR", nameof(TaxAndExpense)),
							new VoucherType("LB", nameof(LB)),
							new VoucherType("PE", nameof(Accrual)),
							new VoucherType("RV", nameof(Revision)),
							new VoucherType("MA", nameof(Anulled)),
						}.ToDictionary(o => o.Code, o => o);
				}
				return _lookup;
			}
		}

		public static VoucherType AR { get => Lookup["AR"]; }
		public static VoucherType AV { get => Lookup["AV"]; }
		public static VoucherType BS { get => Lookup["BS"]; }
		public static VoucherType CR { get => Lookup["CR"]; }
		public static VoucherType KB { get => Lookup["KB"]; } //Expense?
		public static VoucherType KR { get => Lookup["KR"]; } //Expense?
		public static VoucherType FAS { get => Lookup["FAS"]; }
		public static VoucherType SLR { get => Lookup["SLR"]; }
		public static VoucherType Salary { get => Lookup["LON"]; }
		public static VoucherType LAN { get => Lookup["LAN"]; }
		public static VoucherType TaxAndExpense { get => Lookup["LR"]; }
		public static VoucherType LB { get => Lookup["LB"]; }
		public static VoucherType Accrual { get => Lookup["PE"]; }
		public static VoucherType Revision { get => Lookup["RV"]; }
		public static VoucherType Anulled { get => Lookup["MA"]; }

		public static VoucherType GetByCode(string code)
		{
			return Lookup.GetValueOrDefault(code, null) ?? throw new NotImplementedException($"{code}");
		}
	}

	/*
Common pattern:

VER LB6297 189 20160627 
TRANS 24400 {} 425.00 20160627 58460163:SBC Sv Bostadsrättscentrum
VER SLR6297 130 20160527 
TRANS 24400 {} -425.00 20160527 SBC Sv Bostadsrättscentrum
TRANS 26410 {} 0.00 20160527 SBC Sv Bostadsrättscentrum
TRANS 63210 {} 425.00 20160527 SBC Sv Bostadsrättscentrum

VER LB6297 66 20160304 
TRANS 24400 {} 497.00 20160304 53072807:Fortum Värme
VER SLR6297 24 20160203 
TRANS 24400 {} -497.00 20160203 Fortum Värme
TRANS 26410 {} 0.00 20160203 Fortum Värme
TRANS 46200 {} 497.00 20160203 Fortum Värme

SLR comes up to 35(?) days before corresponding LB
For each SLR, check 35 days ahead for LB with same amount and same entry for TRANS, account 24400

*/

	//[DebuggerTypeProxy(typeof(VoucherRecordDebugView))]
	[DebuggerDisplay("{Tag} {VoucherTypeCode} {FormatDate(Date)} {GetTransactionsMaxAmount()} {GetTransactionsCompanyName()}")]
	public class VoucherRecord : SIERecord, IWithChildren
	{
		//#VER AR6297 1 20190210 ""
		//{
		//	#TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		//}
		private static Regex rxType = new Regex(@"(\D+)(\d+)");

		public string VoucherTypeCode { get; set; } = string.Empty;
		public SIE.VoucherType VoucherType { get => SIE.VoucherType.GetByCode(VoucherTypeCode); }
		public string VoucherForId { get; set; } = string.Empty;
		public int Unknown1 { get; set; }
		public LocalDate Date { get; set; }
		public string Unknown2 { get; set; } = string.Empty;

		public override string Tag { get => "VER"; }

		public List<SIERecord> Children { get; set; } = new List<SIERecord>();

		public List<TransactionRecord> Transactions
		{
			get => Children.Where(o => o is TransactionRecord).Cast<TransactionRecord>().ToList();
			set => Children = Children.Where(o => !(o is TransactionRecord)).Concat(value).ToList();
		}
		public IEnumerable<TransactionRecord> TransactionsNonAdmin { get => Transactions.Where(o => !o.IsAdminAccount); }
		public IEnumerable<TransactionRecord> TransactionsNonAdminOrCorrections { get =>
				TransactionsNonAdmin.GroupBy(o => $"{o.AccountId}{Math.Abs(o.Amount)}")
				.Where(o => o.ToList().Sum(p => p.Amount) != 0).SelectMany(o => o.ToList()).ToList(); }

		public string CompanyName { get => Transactions.Select(o => o.CompanyName).Where(o => !string.IsNullOrEmpty(o)).Distinct().Single(); }

		public override void Read(string[] cells)
		{
			var match = rxType.Match(cells[1]);
			VoucherTypeCode = match?.Groups[1].Value ?? "N/A";
			VoucherForId = match?.Groups[2].Value ?? "";
			Unknown1 = int.Parse(cells[2]);
			Date = ParseDate(cells[3]);
			Unknown2 = cells[4].Trim('"');
		}
		public override string ToString()
		{
			return $"{Tag} {VoucherTypeCode}{VoucherForId} {Unknown1} {FormatDate(Date)} {Unknown2}";
		}

		public string GetTransactionsCompanyName() => Transactions.FirstOrDefault()?.CompanyName ?? string.Empty;
		public decimal GetTransactionsMaxAmount() => Transactions.Select(o => Math.Abs(o.Amount)).Max();

		public static void NormalizeCompanyNames(IEnumerable<VoucherRecord> vouchers)
		{
			//Match SLR/LR and other entries
			//var grouped = vouchers.Where(o => o.Transactions.Any()).GroupBy(o => o.Transactions.First().CompanyName).ToDictionary(o => o.Key, o => o.ToList());
			//var sortedNames = grouped.Keys.OrderBy(o => o).ToList();
			var sortedNames = vouchers.SelectMany(o => o.Transactions).Select(o => o.CompanyName).Distinct().OrderBy(o => o).ToList();

			//Test: thought I'd find some groupings with shortened names, but it's not obvious how... var countByLength = sortedNames.GroupBy(o => o.Length).ToDictionary(o => o.Key, o => o.ToList());

			var aliases = new Dictionary<string, List<string>>();
			var shortName = "-----------";
			for (int i = 0; i < sortedNames.Count; i++)
			{
				var name = sortedNames[i];
				if (name.Length < 5)
				{ }
				else if (name.StartsWith(shortName))
				{
					if (!aliases.TryGetValue(shortName, out var list))
					{
						list = new List<string>();
						aliases.Add(shortName, list);
					}
					list.Add(name);
				}
				else
					shortName = name;
			}
			//Create dictionary short -> longest alias (may be multiple short forms)
			var shortToLong = new Dictionary<string, string>();
			foreach (var kv in aliases)
			{
				var longest = kv.Value.OrderByDescending(o => o.Length).First();
				var shorter = kv.Value.Except(new[] { longest }).Concat(new[] { kv.Key });
				foreach (var shrt in shorter)
					shortToLong.Add(shrt, longest);
			}

			//Replace short names with longest:
			vouchers.SelectMany(o => o.Transactions).ToList().ForEach(o => {
				if (shortToLong.TryGetValue(o.CompanyName, out var longest))
					o.CompanyName = longest;
			});
		}

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
		}

		public static VoucherType[] DefaultIgnoreVoucherTypes = new VoucherType[] { VoucherType.AV, VoucherType.BS, VoucherType.FAS, VoucherType.Anulled, VoucherType.Accrual, VoucherType.CR };

		/// <summary>
		/// Attempts to match invoices (SLR, more?) with payments (LB)
		/// </summary>
		/// <param name="vouchers"></param>
		/// <param name="filterVoucherTypes"></param>
		/// <returns></returns>
		public static MatchSLRResult MatchSLRVouchers(IEnumerable<VoucherRecord> vouchers, IEnumerable<VoucherType>? filterVoucherTypes = null)
		{
			if (filterVoucherTypes != null)
				vouchers = vouchers.Where(vr => !filterVoucherTypes.Contains(vr.VoucherType));

			var result = new MatchSLRResult();
			NormalizeCompanyNames(vouchers);

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

			decimal FindRelevantAmount(IEnumerable<TransactionRecord> txs) => txs.Select(o => Math.Abs(o.Amount)).Max();
		}

		//internal class VoucherRecordDebugView { }
	}

	public class TransactionRecord : SIERecord
	{
		// #TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		public override string Tag { get => "TRANS"; }
		public int AccountId { get; set; }
		public string Unknown { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public LocalDate Date { get; set; }
		public string CompanyName { get; set; } = string.Empty;
		public string CompanyId { get; set; } = string.Empty;
		public override void Read(string[] cells)
		{
			AccountId = int.Parse(cells[1]);
			Unknown = cells[2];
			Amount = ParseDecimal(cells[3]);
			Date = ParseDate(cells[4]);
			CompanyName = cells[5].Trim('"');
		}

		/// <summary>
		/// 1**** and 2**** accounts
		/// </summary>
		public bool IsAdminAccount { get => AccountId / 10000 <= 2; } //TODO: better name


		public override string ToString() => $"{Tag} {AccountId} {Unknown} {Amount} {FormatDate(Date)} {CompanyId}{(string.IsNullOrEmpty(CompanyId) ? "" : ":")}{CompanyName}";

		/// <summary>
		/// Remove those with same account where total sum is 0
		/// </summary>
		/// <param name="records"></param>
		/// <returns></returns>
		public static List<TransactionRecord> PruneCorrections(IEnumerable<TransactionRecord> records)
		{
			var doubles = records.GroupBy(o => $"{o.AccountId}_{Math.Abs(o.Amount)}").Where(g => g.Sum(o => o.Amount) == 0);
			return records.Except(doubles.SelectMany(o => o.ToList())).ToList();
		}

	}
}
