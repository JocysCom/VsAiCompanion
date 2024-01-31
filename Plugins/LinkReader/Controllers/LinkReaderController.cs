using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.PowerShellExecutor.Controllers
{
	/// <summary>
	/// Read URL links and return the results.
	/// Allows AI to browse on the Internet.
	/// Use this API with caution due to security risks.
	/// </summary>
	[ApiController]
	[Route("[controller]")]
	public class LinkReaderController : ControllerBase
	{
		/// <summary>
		/// Read the link.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[HttpPost]
		[Route("execute")]
		public async Task<ActionResult<string>> ReadLink(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				return BadRequest("URL is required.");
			}
			try
			{
				string output = await GetWebPageContents(url);
				return Ok(output);
			}
			catch (Exception ex)
			{
				// Log the exception as needed
				return StatusCode(500, $"An error occurred while reading the link: {ex.Message}");
			}
		}


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