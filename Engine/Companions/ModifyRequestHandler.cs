using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{

	/// <summary>
	/// This handler lets add custom properties to the request head.
	/// </summary>
	public class ModifyRequestHandler : DelegatingHandler
	{
		private readonly IDictionary<string, string> LinkProperties;
		private readonly IDictionary<string, string> HeadRequestProperties;
		private readonly IDictionary<string, string> HeadContentProperties;
		private readonly IDictionary<string, string> BodyProperties;

		public ModifyRequestHandler(
			IDictionary<string, string> linkProperties,
			IDictionary<string, string> headRequestProperties,
			IDictionary<string, string> headContentProperties,
			IDictionary<string, string> bodyProperties,
			HttpMessageHandler innerHandler = null)
			: base(innerHandler ?? new HttpClientHandler())
		{
			LinkProperties = linkProperties;
			HeadRequestProperties = headRequestProperties;
			HeadContentProperties = headContentProperties;
			BodyProperties = bodyProperties;
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// Set link properties.
			if (request.RequestUri != null && LinkProperties?.Count > 0)
			{
				var uri = request.RequestUri;
				var uriBuilder = new UriBuilder(uri);
				NameValueCollection queryParams = HttpUtility.ParseQueryString(uriBuilder.Query);
				foreach (var prop in LinkProperties)
					queryParams.Set(prop.Key, prop.Value);
				uriBuilder.Query = queryParams.ToString();
				request.RequestUri = uriBuilder.Uri;
			}
			// Set request head properties.
			if (request.Headers != null && HeadRequestProperties?.Count > 0)
			{
				var headers = request.Headers;
				foreach (var prop in HeadRequestProperties)
				{
					if (headers.Contains(prop.Key))
						headers.Remove(prop.Key);
					headers.Add(prop.Key, prop.Value);
				}
			}
			// Set content head properties.
			if (request.Content.Headers != null && HeadContentProperties?.Count > 0)
			{
				var headers = request.Content.Headers;
				foreach (var prop in HeadContentProperties)
				{
					if (headers.Contains(prop.Key))
						headers.Remove(prop.Key);
					headers.Add(prop.Key, prop.Value);
				}
			}
			// Set body properties.
			if (request.Content != null && BodyProperties?.Count > 0)
			{
				var originalJson = await request.Content.ReadAsStringAsync();
				var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson)
							  ?? new Dictionary<string, object>();
				foreach (var prop in BodyProperties)
					jsonDoc[prop.Key] = prop.Value;
				var newJson = JsonSerializer.Serialize(jsonDoc);
				request.Content = new StringContent(newJson, Encoding.UTF8, "application/json");
			}
			// Pass on the changed request
			return await base.SendAsync(request, cancellationToken);
		}
	}
}
