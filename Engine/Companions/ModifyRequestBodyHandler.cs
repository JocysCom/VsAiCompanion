using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{

	/// <summary>
	/// This handler lets add custom properties to the request JSON body.
	/// </summary>
	public class ModifyRequestBodyHandler : DelegatingHandler
	{
		private readonly IDictionary<string, string> properties;

		public ModifyRequestBodyHandler(IDictionary<string, string> properties, HttpMessageHandler innerHandler)
			: base(innerHandler)
		{
			this.properties = properties;
		}

		public ModifyRequestBodyHandler(IDictionary<string, string> properties)
			: base(new HttpClientHandler())
		{
			this.properties = properties;
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// 1) Check if request.Content != null
			if (request.Content != null)
			{
				// 2) Read the JSON
				var originalJson = await request.Content.ReadAsStringAsync();
				// 3) Deserialize into an object and modify
				var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson)
							  ?? new Dictionary<string, object>();
				// 4) Add your custom property
				foreach (var property in properties)
					jsonDoc[property.Key] = property.Value;
				// 5) Re-serialize
				var newJson = JsonSerializer.Serialize(jsonDoc);
				request.Content = new StringContent(newJson, Encoding.UTF8, "application/json");
			}
			// Pass on the changed request
			return await base.SendAsync(request, cancellationToken);
		}
	}
}
