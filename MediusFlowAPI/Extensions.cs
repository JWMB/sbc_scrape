using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MediusFlowAPI
{
	public static class Extensions
	{
		public static string ToUtcString(this DateTime datetime) => datetime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

		static Regex rxMediusDate = new Regex(@"/Date\((\d{12,14})([+-]\d{4})?\)/");
		public static DateTime? FromMediusDate(this string str)
		{
			if (str == null)
				return null;
			var m = rxMediusDate.Match(str);
			if (m.Success && m.Groups.Count > 1)
				return (DateTime?)new DateTime(1970, 1, 1).AddMilliseconds(ulong.Parse(m.Groups[1].Value)); //TODO: timezone (Groups[2])
			return DateTime.Parse(str);
		}

		public static string ToMediusDate(this DateTime datetime)
		{
			return $"/Date({datetime.ToUnixTimestamp()})/";
		}

		public static long ToUnixTimestamp(this DateTime datetime)
		{
			return (long)(datetime - new DateTime(1970, 1, 1)).TotalMilliseconds;
		}
	}
}
