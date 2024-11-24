using System;
using System.Collections.Generic;
using System.Linq;

namespace JocysCom.ClassLibrary
{

	/// <summary>
	/// Set of methods to manipulate time values.
	/// </summary>
	public static class TimeUnitHelper
	{

		/// <summary>GetTime Values.</summary>
		public static List<DateTime> GetDateTimes(
			DateTime start, int count,
			TimeUnitType unit = TimeUnitType.Day,
			bool preserveEndOfMonth = false
		)
		{
			var range = Enumerable.Range(0, count);
			switch (unit)
			{
				case TimeUnitType.Millisecond:
					return range.Select(i => start.AddMilliseconds(i)).ToList();
				case TimeUnitType.Second:
					return range.Select(i => start.AddSeconds(i)).ToList();
				case TimeUnitType.Minute:
					return range.Select(i => start.AddMinutes(i)).ToList();
				case TimeUnitType.Hour:
					return range.Select(i => start.AddHours(i)).ToList();
				case TimeUnitType.Day:
					return range.Select(i => start.AddDays(i)).ToList();
				case TimeUnitType.Week:
					return range.Select(i => start.AddDays(i * 7)).ToList();
				case TimeUnitType.Fortnight:
					return range.Select(i => start.AddDays(i * 14)).ToList();
				case TimeUnitType.Month:
					return range.Select(i => AddMonths(start, i, preserveEndOfMonth)).ToList();
				case TimeUnitType.Quarter:
					return range.Select(i => AddMonths(start, i * 3, preserveEndOfMonth)).ToList();
				case TimeUnitType.Semester:
					return range.Select(i => AddMonths(start, i * 6, preserveEndOfMonth)).ToList();
				case TimeUnitType.Year:
					return range.Select(i => start.AddYears(i)).ToList();
				case TimeUnitType.Biennial:
					return range.Select(i => start.AddYears(i * 2)).ToList();
				case TimeUnitType.Triennial:
					return range.Select(i => start.AddYears(i * 3)).ToList();
				case TimeUnitType.Decade:
					return range.Select(i => start.AddYears(i * 10)).ToList();
				case TimeUnitType.Century:
					return range.Select(i => start.AddYears(i * 100)).ToList();
				default:
					throw new NotImplementedException();
			}
		}

		/// <summary>GetTime Span.</summary>
		public static TimeSpan GetInterval(int count, TimeUnitType unit = TimeUnitType.Day)
		{
			switch (unit)
			{
				case TimeUnitType.Millisecond:
					return new TimeSpan(0, 0, 0, 0, count);
				case TimeUnitType.Second:
					return new TimeSpan(0, 0, 0, count, 0);
				case TimeUnitType.Minute:
					return new TimeSpan(0, 0, count, 0, 0);
				case TimeUnitType.Hour:
					return new TimeSpan(0, count, 0, 0, 0);
				case TimeUnitType.Day:
					return new TimeSpan(count, 0, 0, 0);
				case TimeUnitType.Week:
					return new TimeSpan(count * 7, 0, 0, 0);
				case TimeUnitType.Fortnight:
					return new TimeSpan(count * 14, 0, 0, 0);
				default:
					throw new NotImplementedException();
			}
		}

		/// <summary>
		/// Add months.
		/// </summary>
		public static DateTime AddMonths(DateTime start, int count, bool preserveEndOfMonth = false)
			=> preserveEndOfMonth && IsEndOfMonth(start)
				? GetEndOfMonth(start.AddMonths(count))
				: start.AddMonths(count);

		/// <summary>
		/// Verify if a date is at the end of month.
		/// </summary>
		public static bool IsEndOfMonth(DateTime date)
			=> date.Day == DateTime.DaysInMonth(date.Year, date.Month);

		/// <summary>
		/// Get last day of month for a date.
		/// </summary>
		public static DateTime GetEndOfMonth(DateTime date)
			=> new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

	}
}
