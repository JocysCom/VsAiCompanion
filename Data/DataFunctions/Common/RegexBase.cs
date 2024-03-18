using Microsoft.SqlServer.Server;
using System.Text.RegularExpressions;

public partial class RegexBase
{

	/// <summary>
	/// Indicates whether the regular expression finds a match in the input string
	/// using the regular expression specified in the pattern parameter.
	/// </summary>
	/// <param name="input">The string to search for a match.</param>
	/// <param name="pattern">The regular expression pattern to match.</param>
	/// <returns>true if the regular expression finds a match; otherwise, false.</returns>
	[SqlFunction]
	public static bool? RegexIsMatch(string input, string pattern)
	{
		if (input == null || pattern == null)
			return null;
		return Regex.IsMatch(input, pattern);
	}

	/// <summary>
	/// Within a specified input string, replaces all strings that match a specified
	/// regular expression with a specified replacement string.
	/// </summary>
	/// <param name="input">The string to search for a match.</param>
	/// <param name="pattern">The regular expression pattern to match.</param>
	/// <param name="replacement">The replacement string.</param>
	/// <returns></returns>
	[SqlFunction]
	public static string RegexReplace(string input, string pattern, string replacement)
	{
		if (input == null || pattern == null || replacement == null)
			return null;
		return Regex.Replace(input, pattern, replacement);
	}

}
