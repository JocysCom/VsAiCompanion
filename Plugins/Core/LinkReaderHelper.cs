using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Helps AI to read links.
	/// </summary>
	public class LinkReaderHelper
	{

		/// <summary>
		/// Use to retrieve content of websites by URL.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		public static async Task<string> GetWebPageContents(string url)
		{
			using (var client = new HttpClient())
			{
				try
				{
					var response = await client.GetAsync(url);
					if (response.IsSuccessStatusCode)
					{
						string content = await response.Content.ReadAsStringAsync();
						return content;
					}
					return $"Error: Unable to fetch the page. Status Code: {response.StatusCode}";
				}
				catch (Exception ex)
				{
					return $"Error: {ex.Message}";
				}
			}
		}


	}
}
