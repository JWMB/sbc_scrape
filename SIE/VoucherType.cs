using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIE
{
	/// <summary>
	/// Are these global values or specific to SBC..?
	/// </summary>
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
		public static VoucherType AV { get => Lookup["AV"]; } // Devaluation / Avskrivning? (e.g. 11228 Avskr. elanläggning + 78220 Förbättringar)
		public static VoucherType BS { get => Lookup["BS"]; } // Maybe end-of-year corrections before finalizing..?
		public static VoucherType CR { get => Lookup["CR"]; }
		public static VoucherType KB { get => Lookup["KB"]; } // Expense?
		public static VoucherType KR { get => Lookup["KR"]; } // Expense?
		public static VoucherType FAS { get => Lookup["FAS"]; } // E.g. 15100 Kundreskontra, 15180 Avräkning rest, 16899 OBS konto, 19710 SBC Klientmedel 1, 30251 Hyror parkering, 30110 Årsavgifter, 83130 Dröjsmålsränta avgifter/hyror
		public static VoucherType SLR { get => Lookup["SLR"]; }
		public static VoucherType Salary { get => Lookup["LON"]; }
		public static VoucherType LAN { get => Lookup["LAN"]; }
		public static VoucherType TaxAndExpense { get => Lookup["LR"]; } // Expense?
		public static VoucherType LB { get => Lookup["LB"]; }
		public static VoucherType Accrual { get => Lookup["PE"]; }
		public static VoucherType Revision { get => Lookup["RV"]; }
		public static VoucherType Anulled { get => Lookup["MA"]; }

		public static VoucherType GetByCode(string code)
		{
			return Lookup.GetValueOrDefault(code, null) ?? throw new NotImplementedException($"{code}");
		}
	}
}
