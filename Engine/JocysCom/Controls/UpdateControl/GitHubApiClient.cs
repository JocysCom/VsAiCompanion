using JocysCom.Controls.UpdateControl.GitHub;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{

	public class GitHubApiClient
	{
		private readonly HttpClient _httpClient;

		public GitHubApiClient()
		{
			_httpClient = new HttpClient();
			// Set the base URL for API requests.
			_httpClient.BaseAddress = new Uri("https://api.github.com/");
			// GitHub API requires a user-agent header.
			_httpClient.DefaultRequestHeaders.Add("User-Agent", "JocysCom GitHub API Client");
		}

		public async Task<List<release>> GetGitHubReleasesAsync(string company, string product)
		{
			var url = $"repos/{company}/{product}/releases";
			var response = await _httpClient.GetAsync(url);
			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync();
				var releases = JsonSerializer.Deserialize<List<release>>(content);
				return releases;
			}
			else
			{
				// Handle non-success status codes appropriately.
				throw new HttpRequestException($"GitHub API request failed with status: {response.StatusCode}");
			}
		}
	}

}
