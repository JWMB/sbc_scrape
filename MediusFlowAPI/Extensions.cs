using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MediusFlowAPI
{
	public static class Extensions
	{
		public static string ToUtcString(this DateTime datetime) => datetime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

		static Regex rxMediusDate = new Regex(@"/Date\((\d{12,14})\)/");
		public static DateTime? FromMediusDate(this string str)
		{
			if (str == null)
				return null;
			var m = rxMediusDate.Match(str);
			return m.Success && m.Groups.Count > 1 ? (DateTime?)new DateTime(1970, 1, 1).AddMilliseconds(ulong.Parse(m.Groups[1].Value)) : null;
		}
	}
}
