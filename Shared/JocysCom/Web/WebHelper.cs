#if ASPNETCORE
using Microsoft.AspNetCore.Http;
#else
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
#endif
using System.Net.Http.Headers;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace JocysCom.ClassLibrary.Web
{
	/// <summary>
	/// Web helper methods.
	/// </summary>
	public class WebHelper
	{
		/// <summary>
		///  Gets the collection of HTTP query string variables.
		/// </summary>
		/// <param name="uriString">A URI.</param>
		/// <returns>Collection of HTTP query string variables.</returns>
		/// <remarks>Similar to System.Web.HttpContext.Current.Request.QueryString method.</remarks>
		public static NameValueCollection GetParamsFromUrl(string uriString)
		{
			return GetParamsFromUrl(uriString, true, Encoding.Default);
		}

		public static NameValueCollection GetParamsFromUrl(string uriString, bool urlencoded, Encoding encoding)
		{

			// <scheme>://<hierarchy>[?<query>][#<fragment>]
			// This will do same thing.
			//System.Web.HttpRequest hr = new System.Web.HttpRequest(string.Empty, "http://localhost", uriString);
			//return hr.Params;
			var results = new NameValueCollection();
			if (string.IsNullOrEmpty(uriString)) return results;
			var schema = new Regex("^[a-z]+://", RegexOptions.IgnoreCase);
			// Remove everything before query. 
			// If this is absolute or full URL then...
			if (schema.IsMatch(uriString) || uriString.StartsWith("/"))
			{
				// And "?" separator is not found then there are no parameters.
				if (!uriString.Contains("?"))
					return results;
			}
			var query = uriString.Substring(uriString.IndexOf('?') + 1);
			// Remove fragment.
			if (query.Contains("#")) query = query.Substring(0, query.IndexOf('#'));
			// return if query is empty.
			if (string.IsNullOrEmpty(query))
				return results;
			var arr = query.Split('&');
			string item;
			string name;
			string value;
			for (var i = 0; i < arr.Length; i++)
			{
				item = arr[i];
				if (item.IndexOf('=') > -1)
				{
					name = item.Substring(0, item.IndexOf('='));
					value = item.Substring(item.IndexOf('=') + 1);
				}
				else
				{
					name = item;
					value = string.Empty;
				}
				if (urlencoded)
				{
					name = HttpUtility.UrlDecode(name, encoding);
					value = HttpUtility.UrlDecode(value, encoding);
				}
				//System.Web.HttpContext.Current.Response.Write("key: " + name + "=" + value + "<br />");
				results.Add(name, value);
			}
			return results;
		}

		public static string DumpHeaders(params HttpHeaders[] headers)
		{
			var list = new List<string>();
			for (var i = 0; i < headers.Length; ++i)
			{
				if (headers[i] is null)
					continue;
				foreach (var keyValuePair in headers[i])
					foreach (string value in keyValuePair.Value)
						list.Add($"{keyValuePair.Key}: {value}");
			}
			return string.Join("\r\n", list);
		}

		public static async Task<NameValueCollection> ToCollection(HttpRequestMessage message)
		{
			var nvc = new NameValueCollection();
			if (message is null)
				return nvc;
			nvc.Add("Type", message.GetType().FullName);
			nvc.Add("RequestHash", string.Format("{0:X4}", message.GetHashCode()));
			nvc.Add("Method", string.Format("{0}", message.Method));
			nvc.Add("URL", string.Format("{0}", message.RequestUri));
			nvc.Add("Version", string.Format("{0}", message.Version));
			nvc.Add("Headers", DumpHeaders(message.Headers));
			var content = message.Content;
			if (content != null)
			{
				nvc.Add("ContentHead", DumpHeaders(content.Headers));
				var body = await content.ReadAsStringAsync();
				nvc.Add("ContentBody", body);
			}
			return nvc;
		}

		public static async Task<NameValueCollection> ToCollection(HttpResponseMessage message, int? requestHshCode = null)
		{
			var nvc = new NameValueCollection();
			if (message is null)
				return nvc;
			nvc.Add("Type", message.GetType().FullName);
			if (requestHshCode.HasValue)
				nvc.Add("RequestHash", string.Format("{0:X4}", requestHshCode.Value));
			nvc.Add("StatusCode", string.Format("{0} - {1}", (int)message.StatusCode, message.StatusCode));
			nvc.Add("StatusName", string.Format("{0}", message.ReasonPhrase));
			nvc.Add("Version", string.Format("{0}", message.Version));
			nvc.Add("Headers", DumpHeaders(message.Headers));
			var content = message.Content;
			if (content != null)
			{
				nvc.Add("ContentHead", DumpHeaders(content.Headers));
				var body = await content.ReadAsStringAsync();
				nvc.Add("ContentBody", body);
			}
			return nvc;
		}

#if ASPNETCORE

		public static string DumpHeaders(params IHeaderDictionary[] headers)
		{
			var list = new List<string>();
			for (var i = 0; i < headers.Length; ++i)
			{
				if (headers[i] is null)
					continue;
				foreach (var keyValuePair in headers[i])
					foreach (string value in keyValuePair.Value)
						list.Add($"{keyValuePair.Key}: {value}");
			}
			return string.Join("\r\n", list);
		}


		public static async Task<NameValueCollection> ToCollection(HttpRequest r)
		{
			var nvc = new NameValueCollection();
			if (r is null)
				return nvc;
			nvc.Add("Type", r.GetType().FullName);
			nvc.Add("RequestHash", string.Format("{0:X4}", r.GetHashCode()));
			nvc.Add("Protocol", $"{r.Protocol}");
			nvc.Add("Method", $"{r.Method}");
			nvc.Add("URL", $"{r.Path}");
			if (r.ContentLength.HasValue)
			{
				nvc.Add("ContentType", $"{r.ContentType}");
				nvc.Add("ContentLength", $"{r.ContentLength}");
			}
			nvc.Add("Headers", "\r\n" + DumpHeaders(r.Headers));
			//var content = r.Body;
			//if (content != null)
			//{
			//	nvc.Add("Body.Length", $"{r.Body.Length}");
			//	var body = await ReadBody(r);
			//	nvc.Add("ContentBody", body);
			//}
			return nvc;
		}

		public static async Task<NameValueCollection> ToCollection(HttpResponse r, int? requestHshCode = null)
		{
			var nvc = new NameValueCollection();
			if (r is null)
				return nvc;
			nvc.Add("Type", r.GetType().FullName);
			if (requestHshCode.HasValue)
				nvc.Add("RequestHash", string.Format("{0:X4}", requestHshCode.Value));
			nvc.Add("StatusCode", $"{r.StatusCode}");
			if (r.ContentLength.HasValue)
			{
				nvc.Add("ContentType", $"{r.ContentType}");
				nvc.Add("ContentLength", $"{r.ContentLength}");
			}
			nvc.Add("Headers", "\r\n" + DumpHeaders(r.Headers));
			//var content = r.Body;
			//if (content != null)
			//{
			//	nvc.Add("Body.Length", $"{r.Body.Length}");
			//	var body = await ReadBody(request);
			//	nvc.Add("ContentBody", body);
			//}
			return nvc;
		}

		static async Task<string> ReadBody(HttpRequest r)
		{
			string s = "";
			// Allows using several time the stream in ASP.Net Core
			//request.EnableRewind();
			await using var writeStream = new MemoryStream();
			await r.BodyReader.CopyToAsync(writeStream);
			// Arguments: Stream, Encoding, detect encoding, buffer size 
			// AND, the most important: keep stream opened
			using (var reader = new StreamReader(r.Body, Encoding.UTF8, true, 1024, true))
				s = reader.ReadToEnd();
			// Rewind, so the core is not lost when it looks the body for the request
			r.Body.Position = 0;
			return s;
		}

#endif

	}
}
