using NodaTime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MediusFlowAPI
{
	public static class Extensions
	{
		public static string ToUtcString(this DateTime datetime) => datetime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

		static readonly Regex  rxMediusDate = new Regex(@"/Date\((\d{12,14})([+-]\d{4})?\)/");

		public static DateTime? FromMediusDate(this string str)
		{
			if (str == null)
				return null;
			var m = rxMediusDate.Match(str);
			if (m.Success && m.Groups.Count > 1)
				return (DateTime?)new DateTime(1970, 1, 1).AddMilliseconds(ulong.Parse(m.Groups[1].Value)); //TODO: timezone (Groups[2])
			return DateTime.Parse(str);
		}

		public static string ToMediusDate(this DateTime datetime) => $"/Date({datetime.ToUnixTimestamp()})/";

		public static LocalDateTime ToLocalDateTime(this DateTime datetime) => LocalDateTime.FromDateTime(datetime);
		public static LocalDateTime? ToLocalDateTime(this DateTime? datetime) => datetime == null ? null : (LocalDateTime?)LocalDateTime.FromDateTime(datetime.Value);

		public static LocalDate ToLocalDate(this DateTime datetime) => LocalDateTime.FromDateTime(datetime).Date;
		public static LocalDate? ToLocalDate(this DateTime? datetime) => datetime == null ? null : (LocalDate?)LocalDateTime.FromDateTime(datetime.Value).Date;


		public static long ToUnixTimestamp(this DateTime datetime) => (long)(datetime - new DateTime(1970, 1, 1)).TotalMilliseconds;
	}
}
