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

}
