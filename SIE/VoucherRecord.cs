using NodaTime;
using System;
using System.Collections.Generic;
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

		private static Dictionary<string, VoucherType> _lookup;
		private static Dictionary<string, VoucherType> Lookup
		{
			get
			{
				if (_lookup == null)
				{
					_lookup = new[] {
							new VoucherType("AR", nameof(AR)),
							new VoucherType("AV", nameof(AV)),
							new VoucherType("BS", nameof(BS)),
							new VoucherType("CR", nameof(CR)),
							new VoucherType("FAS", nameof(FAS)),
							new VoucherType("SLR", nameof(SLR)),
							new VoucherType("LON", nameof(Salary)),
							new VoucherType("LR", nameof(TaxAndExpense)),
							new VoucherType("PE", nameof(Accrual)),
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
		public static VoucherType FAS { get => Lookup["FAS"]; }
		public static VoucherType SLR { get => Lookup["SLR"]; }
		public static VoucherType Salary { get => Lookup["LON"]; }
		public static VoucherType TaxAndExpense { get => Lookup["LR"]; }
		public static VoucherType Accrual { get => Lookup["PE"]; }
		public static VoucherType Anulled { get => Lookup["MA"]; }

		public static VoucherType GetByCode(string code)
		{
			return Lookup.GetValueOrDefault(code, null);
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

	public class VoucherRecord : SIERecord, IWithChildren
	{
		//#VER AR6297 1 20190210 ""
		//{
		//	#TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		//}
		private static Regex rxType = new Regex(@"(\D+)(\d+)");

		public string VoucherTypeCode { get; set; }
		public SIE.VoucherType VoucherType { get => SIE.VoucherType.GetByCode(VoucherTypeCode); }
		public string VoucherForId { get; set; }
		public int Unknown1 { get; set; }
		public LocalDate Date { get; set; }
		public string Unknown2 { get; set; }

		public override string Tag { get => "VER"; }

		public List<SIERecord> Children { get; set; } = new List<SIERecord>();

		public List<TransactionRecord> Transactions { get => Children.Where(o => o is TransactionRecord).Cast<TransactionRecord>().ToList(); }
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

		public static void NormalizeCompanyNames(IEnumerable<VoucherRecord> vouchers)
		{
			//Match SLR and LB entries
			var grouped = vouchers.GroupBy(o => o.Transactions.First().CompanyName).ToDictionary(o => o.Key, o => o.ToList());
			var sortedNames = grouped.Keys.OrderBy(o => o).ToList();

			var aliases = new Dictionary<string, List<string>>();
			var shortName = "-----------";
			for (int i = 0; i < sortedNames.Count; i++)
			{
				var name = sortedNames[i];
				if (name.Length < 5)
				{ }
				if (name.StartsWith(shortName))
				{
					var list = new List<string>();
					aliases.Add(shortName, list);
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
	}

	public class TransactionRecord : SIERecord
	{
		// #TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		public override string Tag { get => "TRANS"; }
		public int AccountId { get; set; }
		public string Unknown { get; set; }
		public decimal Amount { get; set; }
		public LocalDate Date { get; set; }
		public string CompanyName { get; set; }
		public string CompanyId { get; set; }
		public override void Read(string[] cells)
		{
			AccountId = int.Parse(cells[1]);
			Unknown = cells[2];
			Amount = ParseDecimal(cells[3]);
			Date = ParseDate(cells[4]);
			CompanyName = cells[5].Trim('"');
			var rx = new Regex(@"^(\d+)(?::)(.+)"); //"12345:abcde"
			var m = rx.Match(CompanyName);
			if (m.Success)
			{
				CompanyId = m.Groups[1].Value;
				CompanyName = m.Groups[2].Value;
			}
			else
			{
				rx = new Regex(@"([^;]+);(.+)"); //SBC\slltbq 160905;CompanyName
				m = rx.Match(CompanyName);
				if (m.Success)
				{
					CompanyId = m.Groups[1].Value;
					CompanyName = m.Groups[2].Value;
				}
			}
		}
		public override string ToString() => $"{Tag} {AccountId} {Unknown} {Amount} {FormatDate(Date)} {CompanyId}{(string.IsNullOrEmpty(CompanyId) ? "" : ":")}{CompanyName}";

		public static List<TransactionRecord> PruneCorrections(IEnumerable<TransactionRecord> records)
		{
			var doubles = records.GroupBy(o => $"{o.AccountId}_{Math.Abs(o.Amount)}").Where(g => g.Sum(o => o.Amount) == 0);
			return records.Except(doubles.SelectMany(o => o.ToList())).ToList();
		}
	}
}
