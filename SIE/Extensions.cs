using System;
using System.Collections.Generic;
using System.Text;

namespace SIE
{
	public static class NodaTimeExtensions
	{
		public static string ToSimpleDateString(this NodaTime.LocalDate date) => date.AtMidnight().ToDateTimeUnspecified().ToString("yyyy-MM-dd");
	}
}
