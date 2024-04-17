using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Web functions that allow AI to retrieve web content or files from the web or call APIs.
	/// </summary>
	public partial class Web
	{

		/// <summary>
		/// Retrieve content of websites by URL.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <param name="asPlainText">Read the content of a page in plain text.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.Low)]
		public async Task<string> GetWebPageContents(string url, bool asPlainText)
		{
			var contents = await _GetWebPageContents(url, false);
			if (contents != null && asPlainText)
				contents = HtmlHelper.ReadHtmlAsPlainText(contents);
			return contents;
		}


		/// <summary>
		/// Retrieve content of websites by URL. Use default credentials of the user.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <param name="asPlainText">Read the content of a page in plain text.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.High)]
		public async Task<string> GetWebPageContentsAuthenticated(string url, bool asPlainText)
		{
			var contents = await _GetWebPageContents(url, true);
			if (contents != null && asPlainText)
				contents = HtmlHelper.ReadHtmlAsPlainText(contents);
			return contents;
		}


		/// <summary>
		/// Download and return the content of a given URL.
		/// </summary>
		/// <param name="url">URL from which the content will be downloaded. The URL can have different schemes like 'https://', 'file://', etc., that are capable of fetching files or data across various protocols.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.High)]
		public static async Task<DocItem> DownloadContent(string url)
		{
			var docItem = new DocItem("", url);
			try
			{
				byte[] content = await _DownloadContent(url, false);
				docItem.LoadData(content);
			}
			catch (Exception ex)
			{
				docItem.Error = ex.Message;
			}
			return docItem;
		}


		/// <summary>
		/// Download and return the content of a given URL. Use default credentials of the user.
		/// </summary>
		/// <param name="url">URL from which the content will be downloaded. The URL can have different schemes like 'https://', 'file://', etc., that are capable of fetching files or data across various protocols.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.High)]
		public static async Task<DocItem> DownloadContentAuthenticated(string url)
		{
			var docItem = new DocItem("", url);
			try
			{
				byte[] content = await _DownloadContent(url, true);
				docItem.LoadData(content);
			}
			catch (Exception ex)
			{
				docItem.Error = ex.Message;
			}
			return docItem;
		}


		/// <summary>
		/// Performs an HTTP request to the specified API and returns its JSON response.
		/// </summary>
		/// <param name="httpCallType">The type of HTTP call (e.g., GET, POST)</param>
		/// <param name="apiBaseUrl">The base URL of the API.</param>
		/// <param name="jsonString">The JSON string to send as the payload, if applicable.</param>
		/// <returns>The JSON response from the API.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static async Task<string> CallApi(
			string httpCallType,
			string apiBaseUrl,
			string jsonString = null
		)
		=> await _CallApiAsync(httpCallType, apiBaseUrl, jsonString, false);

		/// <summary>
		/// Performs an HTTP request to the specified API and returns its JSON response.
		/// </summary>
		/// <param name="httpCallType">The type of HTTP call (e.g., GET, POST)</param>
		/// <param name="apiBaseUrl">The base URL of the API.</param>
		/// <param name="jsonString">The JSON string to send as the payload, if applicable.</param>
		/// <returns>The JSON response from the API.</returns>
		[RiskLevel(RiskLevel.High)]
		public static async Task<string> CallApiAuthenticated(
			string httpCallType,
			string apiBaseUrl,
			string jsonString = null
		)
		=> await _CallApiAsync(httpCallType, apiBaseUrl, jsonString, true);

		#region Helper functions

		private static async Task<string> _GetWebPageContents(string url, bool useDefaultCredentials)
		{
			var handler = new HttpClientHandler()
			{
				// Enable the usage of the default credentials of the current user.
				UseDefaultCredentials = useDefaultCredentials
			};
			using (var client = new HttpClient(handler))
			{
				try
				{
					var response = await client.GetAsync(url);
					if (response.IsSuccessStatusCode)
					{
						// Successfully fetched the content.
						string content = await response.Content.ReadAsStringAsync();
						return content;
					}
					// Handle responses that are considered as unsuccessful.
					return $"Error: Unable to fetch the page. Status Code: {response.StatusCode}";
				}
				catch (Exception ex)
				{
					// Handle any exceptions that occur during the request.
					return $"Error: {ex.Message}";
				}
			}
		}

		private static async Task<byte[]> _DownloadContent(string url, bool useDefaultCredentials)
		{
			var handler = new HttpClientHandler()
			{
				// Use default credentials for domain authentication if needed.
				UseDefaultCredentials = useDefaultCredentials
			};
			using (var client = new HttpClient(handler))
			{
				try
				{
					// Send asynchronous request to download the file content
					using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						// Read the content as byte array
						byte[] content = await response.Content.ReadAsByteArrayAsync();
						return content;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"An error occurred while downloading the file: {ex.Message}");
					throw;
				}
			}
		}


		/// <summary>
		/// Performs an HTTP request to the specified API and returns its JSON response.
		/// </summary>
		/// <param name="httpCallType">The type of HTTP call (e.g., GET, POST)</param>
		/// <param name="apiBaseUrl">The base URL of the API.</param>
		/// <param name="jsonString">The JSON string to send as the payload, if applicable.</param>
		/// <param name="useDefaultCredentials">Use default credentials.</param>
		/// <returns>The JSON response from the API.</returns>
		private static async Task<string> _CallApiAsync(
			string httpCallType,
			string apiBaseUrl,
			string jsonString = null,
			bool useDefaultCredentials = false
		)
		{
			var handler = new HttpClientHandler()
			{
				UseDefaultCredentials = useDefaultCredentials
			};
			using (var client = new HttpClient(handler))
			{
				var method = new HttpMethod(httpCallType);
				var request = new HttpRequestMessage(method, apiBaseUrl)
				{
					Headers = {
						Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
					}
				};
				// Including DELETE in the methods that are allowed to have a body.
				if (!string.IsNullOrWhiteSpace(jsonString) &&
					(method == HttpMethod.Post ||
					 method == HttpMethod.Put ||
					 method == HttpMethod.Get ||
					 method == HttpMethod.Delete))
				{
					request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
				}
				try
				{
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
					return await response.Content.ReadAsStringAsync();
				}
				catch (HttpRequestException e)
				{
					throw new Exception($"HttpRequestException: {e.Message}");
				}
				catch (Exception e)
				{
					throw new Exception($"Unhandled exception: {e.Message}");
				}
			}
		}

		#endregion

	}
}
