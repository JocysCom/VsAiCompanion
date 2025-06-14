using JocysCom.ClassLibrary.Web.Services;
using OpenAI;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// HTTP client configuration and transport management for ChatGPT client.
	/// </summary>
	public partial class Client
	{
		#region HTTP Client Management

		/// <summary>
		/// Creates and configures an HttpClient for API communication.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token for the operation.</param>
		/// <returns>Configured HttpClient instance.</returns>
		public async Task<HttpClient> GetClient(CancellationToken cancellationToken = default)
		{
			_Logger = new HttpClientLogger();
			var client = new HttpClient(_Logger);
			client.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			client.BaseAddress = new Uri(Service.BaseUrl);
			var apiSecretKey = await Security.MicrosoftResourceManager.Current.GetKeyVaultSecretValue(Service.ApiSecretKeyVaultItemId, Service.ApiSecretKey);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiSecretKey);
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			var apiOrganizationId = await Security.MicrosoftResourceManager.Current.GetKeyVaultSecretValue(Service.ApiOrganizationIdVaultItemId, Service.ApiOrganizationId);
			if (!string.IsNullOrEmpty(apiOrganizationId))
				client.DefaultRequestHeaders.Add("OpenAI-Organization", apiOrganizationId);
			return client;
		}

		/// <summary>
		/// Gets an AI client instance with proper configuration and authentication.
		/// </summary>
		/// <param name="useLogger">Whether to enable HTTP logging.</param>
		/// <param name="item">Template item for additional configuration.</param>
		/// <param name="cancellationToken">Cancellation token for the operation.</param>
		/// <returns>Configured OpenAI client instance.</returns>
		public async Task<OpenAIClient> GetAiClient(bool useLogger = true, TemplateItem item = null, CancellationToken cancellationToken = default)
		{
			// https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme?view=azure-dotnet-preview
			// https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI/src
			var endpoint = new Uri(Service.BaseUrl);
			var apiSecretKey = await Security.MicrosoftResourceManager.Current.GetKeyVaultSecretValue(Service.ApiSecretKeyVaultItemId, Service.ApiSecretKey);
			OpenAIClient client;

			// Initialize logger if requested
			if (useLogger)
				_Logger = new HttpClientLogger();

			// If use modified OpenAIClient by Microsoft then...
			if (Service.IsAzureOpenAI)
			{
				// Using `Azure.AI.OpenAI.AzureOpenAIClient` by `azure-sdk Microsoft`
				var options = new Azure.AI.OpenAI.AzureOpenAIClientOptions();
				options.NetworkTimeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
				options.Transport = GetTransport(item, useLogger);
				if (string.IsNullOrEmpty(apiSecretKey))
				{
					var credential = new Azure.Identity.DefaultAzureCredential();
					client = new Azure.AI.OpenAI.AzureOpenAIClient(endpoint, credential, options);
				}
				else
				{
					var credential = new System.ClientModel.ApiKeyCredential(apiSecretKey);
					client = new Azure.AI.OpenAI.AzureOpenAIClient(endpoint, credential, options);
				}
			}
			else
			{
				// Using `OpenAI.OpenAIClient` by `OpenAIOfficial`
				var options = new OpenAIClientOptions();
				options.NetworkTimeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
				options.Endpoint = endpoint;
				options.Transport = GetTransport(item, useLogger);
				if (string.IsNullOrEmpty(apiSecretKey))
					apiSecretKey = "NoKey";
				var credential = new System.ClientModel.ApiKeyCredential(apiSecretKey);
				client = new OpenAIClient(credential, options);
			}
			return client;
		}

		/// <summary>
		/// Creates a pipeline transport with custom request modification capabilities.
		/// </summary>
		/// <param name="item">Template item for configuration.</param>
		/// <param name="useLogger">Whether to enable HTTP logging.</param>
		/// <returns>Configured pipeline transport.</returns>
		public PipelineTransport GetTransport(TemplateItem item = null, bool useLogger = false)
		{
			var endpoint = new Uri(Service.BaseUrl);
			// Add/override link query properties.
			var requestQueryData = new Dictionary<string, string>();
			if (item?.AiService?.IsAzureOpenAI == true && item?.AiService?.OverrideApiVersionEnabled == true)
				requestQueryData.Add("api-version", item.AiService.OverrideApiVersion);
			foreach (var kv in item.RequestQueryData.Items.Where(x => !string.IsNullOrEmpty(x.Key)))
				requestQueryData.Add(kv.Key, kv.Value);
			// Add/override request header properties.
			var requestHeaders = new Dictionary<string, string>();
			foreach (var kv in item.RequestHeaders.Items.Where(x => !string.IsNullOrEmpty(x.Key)))
				requestHeaders.Add(kv.Key, kv.Value);
			// Add/override content header properties.
			var requestContentHeaders = new Dictionary<string, string>();
			foreach (var kv in item.RequestContentHeaders.Items.Where(x => !string.IsNullOrEmpty(x.Key)))
				requestContentHeaders.Add(kv.Key, kv.Value);
			//headContentProperties.Add("Content-Type", "application/json");
			// Add/override body properties.
			var requestBodyData = new Dictionary<string, string>();
			if (item != null && item.ReasoningEffort != reasoning_effort.medium)
				requestBodyData.Add(nameof(reasoning_effort), item.ReasoningEffort.ToString());
			foreach (var kv in item.RequestBodyData.Items.Where(x => !string.IsNullOrEmpty(x.Key)))
				requestBodyData.Add(kv.Key, kv.Value);
			// Always create a custom transport so the pipeline is used.
			HttpMessageHandler handler = new HttpClientHandler();
			// Add logger if requested
			if (useLogger && _Logger != null)
				handler = _Logger;
			handler = new ModifyRequestHandler(requestQueryData, requestHeaders, requestContentHeaders, requestBodyData, handler);
			var httpClient = new HttpClient(handler);
			httpClient.BaseAddress = endpoint;
			httpClient.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			// Register the handler in the HttpPipeline
			var transport = new HttpClientPipelineTransport(httpClient);
			return transport;
		}

		/// <summary>
		/// Creates a pipeline transport with legacy parameter support.
		/// </summary>
		/// <param name="item">Template item for configuration.</param>
		/// <returns>Configured pipeline transport.</returns>
		public PipelineTransport GetTransport(TemplateItem item = null)
		{
			var endpoint = new Uri(Service.BaseUrl);
			// Add/override link query properties.
			var linkProperties = new Dictionary<string, string>();
			if (item?.AiService?.IsAzureOpenAI == true && item?.AiService?.OverrideApiVersionEnabled == true)
				linkProperties.Add("api-version", item.AiService.OverrideApiVersion);
			// Add/override request header properties.
			var headRequestProperties = new Dictionary<string, string>();
			// Add/override content header properties.
			var headContentProperties = new Dictionary<string, string>();
			//headContentProperties.Add("Content-Type", "application/json");
			// Add/override body properties.
			var bodyProperties = new Dictionary<string, string>();
			if (item != null && item.ReasoningEffort != reasoning_effort.medium)
				bodyProperties.Add(nameof(reasoning_effort), item.ReasoningEffort.ToString());
			// Always create a custom transport so the pipeline is used.
			HttpMessageHandler handler = new HttpClientHandler();
			//if (useLogger)
			//	handler = new HttpClientLogger(handler);
			handler = new ModifyRequestHandler(linkProperties, headRequestProperties, headContentProperties, bodyProperties, handler);
			var httpClient = new HttpClient(handler);
			httpClient.BaseAddress = endpoint;
			httpClient.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			// Register the handler in the HttpPipeline
			var transport = new HttpClientPipelineTransport(httpClient);
			return transport;
		}

		#endregion
	}
}
