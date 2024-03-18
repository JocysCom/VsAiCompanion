using Microsoft.SqlServer.Server;

public partial class StringBase
{

	[SqlFunction] public static string HtmlDecode(string value) => System.Net.WebUtility.HtmlDecode(value);
	[SqlFunction] public static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);
	[SqlFunction] public static string UrlDecode(string value) => System.Net.WebUtility.UrlDecode(value);
	[SqlFunction] public static string UrlEncode(string value) => System.Net.WebUtility.UrlEncode(value);

	[SqlFunction]
	public static string UrlEncodeKeyValue(string key, string value)
		=> string.Format("&{0}={1}", System.Net.WebUtility.UrlEncode(key), System.Net.WebUtility.UrlEncode(value));

	/// <summary>
	/// Remove diacritic marks (accent marks) from characters and convert to ASCII.
	/// "Fuerza Aérea (Edificio Cóndor) Heliport" -> "Fuerza Aerea (Edificio Condor) Heliport"
	/// </summary>
	/// <param name="input">The string to process.</param>
	[SqlFunction]
	public static string ConvertToASCII(string input)
	{
		return JocysCom.ClassLibrary.Text.Filters.ConvertToASCII(input);
	}

	/// <summary>
	/// Convert text to ASCII key.
	/// "Fuerza Aérea (Edificio Cóndor) Heliport" -> "Fuerza_Aerea_Edificio_Condor_Heliport"
	/// </summary>
	/// <param name="input">The string to convert.</param>
	[SqlFunction]
	public static string GetTitleKey(string input, bool capitalize)
	{
		return JocysCom.ClassLibrary.Text.Filters.GetKey(input, capitalize);
	}
}
