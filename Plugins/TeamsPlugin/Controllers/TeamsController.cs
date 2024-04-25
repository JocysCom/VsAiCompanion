using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.TeamsPlugin.Controllers
{
	/// <summary>
	/// Allows to request information and data from Microsoft Teams.
	/// </summary>
	[ApiController]
	[Route("[controller]")]
	public class TeamsController : ControllerBase
	{

		private readonly IConfiguration _configuration;

		/// <summary>
		/// Allows to request information and data from Microsoft Teams.
		/// </summary>
		public TeamsController(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		// https://developer.microsoft.com/en-us/graph/graph-explorer

		private const string msUrl = "https://login.microsoftonline.com";
		// {0} - Directory (tenant) ID
		private const string OAuth2V2Authorize = msUrl + "/{0}/oauth2/v2.0/authorize";
		private const string OAuth2V2Token = msUrl + "/{0}/oauth2/v2.0/token";
		private const string OAuth2V1Authorize = msUrl + "/{0}/oauth2/authorize";
		private const string OAuth2V1Token = msUrl + "/{0}/oauth2/token";
		private const string OpenIdConnectMetadata = msUrl + "/{0}/v2.0/.well-known/openid-configuration";
		private const string FederationMetadata = msUrl + "/{0}/federationmetadata/2007-06/federationmetadata.xml";
		private const string WsFederationSignOn = msUrl + "/{0}/wsfed";
		private const string SamlPSignOn = msUrl + "/{0}/saml2";
		private const string SamlPSignOut = msUrl + "/{0}/saml2";
		private const string GraphAPI = "https://graph.microsoft.com";

		/// <summary>
		/// Get list of meetings.
		/// </summary>
		/// <returns>The output of request.</returns>
		/// <exception cref="Exception">Error message explaining why the request failed.</exception>
		[Route("ListMeetings")]
		[HttpPost]
		public async Task<ActionResult<string>> ListMeetings()
		{
			var tenantId = _configuration["AzureAd:TenantId"];
			var clientId = _configuration["AzureAd:ClientId"];
			var clientSecret = _configuration["AzureAd:ClientSecret"];

			IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
				.WithClientSecret(clientSecret)
				.WithAuthority($"{msUrl}/{tenantId}")
				.Build();
			var authResult = await app.AcquireTokenForClient(new[] { $"{GraphAPI}/.default" }).ExecuteAsync();
			var httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

			// Adjust the URL as needed; this example lists the signed-in user's events
			var response = await httpClient.GetAsync($"{GraphAPI}/v1.0/me/events");

			if (response.IsSuccessStatusCode)
			{
				var contentString = await response.Content.ReadAsStringAsync();
				return Ok(contentString);
			}
			else
			{
				return Problem(response.ToString());
			}
		}


		/// <summary>
		/// Get list of meetings.
		/// </summary>
		/// <param name="tokenAcquisition"></param>
		/// <exception cref="Exception">Error message explaining why the request failed.</exception>
		/// <returns>The output of request.</returns>
		[HttpPost]
		[Route("ListMeetingsAuthorized")]
		[Authorize]
		public async Task<ActionResult<string>> ListMeetingsAuthorized([FromServices] ITokenAcquisition tokenAcquisition)
		{
			var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(new[] { $"{GraphAPI}/Calendars.Read" });

			// Use accessToken to call Microsoft Graph
			var httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var response = await httpClient.GetAsync($"{GraphAPI}/v1.0/me/events?$select=subject,body,bodyPreview,organizer,attendees,start,end,location");
			if (response.IsSuccessStatusCode)
			{
				string contentString = await response.Content.ReadAsStringAsync();
				// Deserialize JSON and use the data as needed
				return Ok(contentString); // or convert it to a model and pass it to a view
			}
			else
			{
				// Log error, handle exception, or return an error message
				return StatusCode((int)response.StatusCode, "Request to Microsoft Graph failed");
			}
		}

	}
}
