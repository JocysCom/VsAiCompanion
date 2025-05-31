using System;
using System.Collections.Generic;
using System.Linq;

namespace JocysCom.ClassLibrary
{

	/// <summary>
	/// Provides utility methods for generating date/time sequences and intervals based on TimeUnitType values.
	/// </summary>
	public static class TimeUnitHelper
	{

		/// <summary>
		/// Generates a list of dates starting at the specified date, incremented by the given time unit.
		/// </summary>
		/// <param name="start">The starting DateTime value.</param>
		/// <param name="count">The number of dates to generate; must be non-negative.</param>
		/// <param name="unit">The unit of time to increment by, as defined in TimeUnitType.</param>
		/// <param name="preserveEndOfMonth">
		/// When true and the start date is the last day of a month, subsequent dates will also be on month ends.
		/// </param>
		/// <returns>A list of DateTime values of length count.</returns>
		/// <remarks>
		/// Throws <see cref="NotImplementedException"/> if unit is not supported.
		/// </remarks>
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

		/// <summary>
		/// Returns a TimeSpan representing the specified count of the given time unit.
		/// </summary>
		/// <param name="count">The number of units; must be non-negative.</param>
		/// <param name="unit">The time unit for the interval, as defined in TimeUnitType.</param>
		/// <returns>A TimeSpan equivalent to count units.</returns>
		/// <remarks>
		/// Supported units: Millisecond, Second, Minute, Hour, Day, Week, Fortnight.
		/// Throws <see cref="NotImplementedException"/> for other units.
		/// </remarks>
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
		/// Adds the specified number of months to a date, optionally preserving end-of-month alignment.
		/// </summary>
		/// <param name="start">The original date.</param>
		/// <param name="count">The number of months to add.</param>
		/// <param name="preserveEndOfMonth">
		/// If true and start is the last day of its month, the result will also be the last day of the target month.
		/// </param>
		/// <returns>A DateTime offset by the given number of months.</returns>
		public static DateTime AddMonths(DateTime start, int count, bool preserveEndOfMonth = false)
			=> preserveEndOfMonth && IsEndOfMonth(start)
				? GetEndOfMonth(start.AddMonths(count))
				: start.AddMonths(count);

		/// <summary>
		/// Determines whether the specified date is the last day of its month.
		/// </summary>
		/// <param name="date">The date to evaluate.</param>
		/// <returns>True if date is the final day of its month; otherwise false.</returns>
		public static bool IsEndOfMonth(DateTime date)
			=> date.Day == DateTime.DaysInMonth(date.Year, date.Month);

		/// <summary>
		/// Returns a DateTime for the last day of the month for a given date.
		/// </summary>
		/// <param name="date">The date whose month-end is desired.</param>
		/// <returns>A DateTime set to the last day of date's month.</returns>
		public static DateTime GetEndOfMonth(DateTime date)
			=> new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

	}
}