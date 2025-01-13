using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{

	/// <summary>
	/// This handler lets add custom properties to the request head.
	/// </summary>
	public class ModifyRequestHeadHandler : DelegatingHandler
	{
		private readonly IDictionary<string, string> properties;

		public ModifyRequestHeadHandler(IDictionary<string, string> properties, HttpMessageHandler innerHandler)
			: base(innerHandler)
		{
			this.properties = properties;
		}

		public ModifyRequestHeadHandler(IDictionary<string, string> properties)
			: base(new HttpClientHandler())
		{
			this.properties = properties;
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.Headers != null)
			{
				foreach (var header in request.Headers)
					request.Headers.Add(header.Key, header.Value);
			}
			// Pass on the changed request
			return await base.SendAsync(request, cancellationToken);
		}
	}
}
