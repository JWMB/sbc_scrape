using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SIE
{
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
		public override string Tag { get => "VER"; }

		//#VER AR6297 1 20190210 ""
		//{
		//	#TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		//}
		private static readonly Regex rxType = new Regex(@"(\D+)(\d+)");

		public string VoucherTypeCode { get; set; } = string.Empty;
		public SIE.VoucherType VoucherType { get => SIE.VoucherType.GetByCode(VoucherTypeCode); }
		public string VoucherForId { get; set; } = string.Empty;
		public int SerialNumber { get; set; }
		public LocalDate Date { get; set; }
		public string Unknown2 { get; set; } = string.Empty;

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

		///// <summary>
		///// Will throw an exception if multiple names found (e.g. in end-of-year revision vouchers)
		///// </summary>
		//public string CompanyName { get => Transactions.Select(o => o.CompanyName).Where(o => !string.IsNullOrEmpty(o)).Distinct().SingleOrDefault() ?? string.Empty; }

		private string? _companyNameCalced = null;
		public string CompanyName
		{
			get {
				if (_companyNameCalced == null)
					_companyNameCalced = Transactions.Select(o => o.CompanyName).OrderByDescending(o => o.Length).First();
				return _companyNameCalced;
			}
		}

		public string Id { get => $"{Date.Year}_{Series}_{SerialNumber}"; }
		public string Series { get => $"{VoucherTypeCode}{VoucherForId}"; }

		public override void Read(string[] cells)
		{
			var match = rxType.Match(cells[1]);
			VoucherTypeCode = match?.Groups[1].Value ?? "N/A";
			VoucherForId = match?.Groups[2].Value ?? "";
			SerialNumber = int.Parse(cells[2]);
			Date = ParseDate(cells[3]);
			Unknown2 = cells[4].Trim('"');
		}
		public override string ToString()
		{
			return $"{Tag} {VoucherTypeCode}{VoucherForId} {SerialNumber} {FormatDate(Date)} {Unknown2}";
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

		public static VoucherType[] DefaultIgnoreVoucherTypes = new VoucherType[] { VoucherType.AV, VoucherType.BS, VoucherType.FAS, VoucherType.Anulled, VoucherType.Accrual, VoucherType.CR };
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
