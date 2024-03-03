using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	public partial class Basic
	{

		/// <summary>
		/// Retrieve content of websites by URL.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.Low)]
		public async Task<string> GetWebPageContents(string url)
			=> await _GetWebPageContents(url, true);

		/// <summary>
		/// Retrieve content of websites by URL. Use default credentials of the user.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.High)]
		public async Task<string> GetWebPageContentsAuthenticated(string url)
			=> await _GetWebPageContents(url, true);

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

		#endregion

	}
}
