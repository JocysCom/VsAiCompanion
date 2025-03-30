using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Web.Services;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class Client : IAiClient
	{
		public Client(AiService service)
		{
			Service = service;
		}
		private const string usagePath = "usage";
		private const string modelsPath = "models";
		private const string filesPath = "files";
		private const string fineTuningJobsPath = "fine_tuning/jobs";
		public const string FineTuningPurpose = "fine-tune";

		private readonly AiService Service;

		/// <summary>
		/// Can be used to log response and reply.
		/// </summary>
		public HttpClientLogger Logger => _Logger;
		HttpClientLogger _Logger;

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

		public static JsonSerializerOptions GetJsonOptions(bool writeIndented = false)
		{
			var o = new JsonSerializerOptions();
			o.WriteIndented = writeIndented;
			o.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
			o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
			o.Converters.Add(new UnixTimestampConverter());
			o.Converters.Add(new JsonStringEnumConverter());
			return o;
		}

		static JsonSerializerOptions JsonOptions
		{
			get
			{
				if (_JsonOptions == null)
					_JsonOptions = GetJsonOptions();
				return _JsonOptions;
			}
		}
		static JsonSerializerOptions _JsonOptions;

		public static T Deserialize<T>(string json)
			=> JsonSerializer.Deserialize<T>(json, JsonOptions);

		public static string Serialize(object o, bool writeIndented = false)
			=> writeIndented
			? JsonSerializer.Serialize(o, GetJsonOptions(writeIndented))
			: JsonSerializer.Serialize(o, JsonOptions);

		public async Task<file> UploadFileAsync(string filePath, string purpose, CancellationToken cancellationToken = default)
		{
			//var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			//var urlWithDate = $"{Service.BaseUrl}{filesPath}?date={date}";
			var urlWithDate = $"{Service.BaseUrl}{filesPath}";
			var client = await GetClient();
			//client.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			using (var content = new MultipartFormDataContent())
			{
				content.Add(new StringContent(purpose), "\"purpose\"");
				var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
				fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
				content.Add(fileContent, "\"file\"", $"\"{Path.GetFileName(filePath)}\"");
				using (var response = await client.PostAsync(urlWithDate, content))
				{
					var responseBody = await response.Content.ReadAsStringAsync();
					if (!response.IsSuccessStatusCode)
					{
						LastError = responseBody;
						return null;
					}
					var responseFile = Deserialize<file>(responseBody);
					return responseFile;
				}
			}
		}

		public async Task<T> DeleteAsync<T>(string path, string id, CancellationToken cancellationToken = default)
		{
			//var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			//var urlWithDate = $"{Service.BaseUrl}{path}/{id}?date={date}";
			var urlWithDate = $"{Service.BaseUrl}{path}/{id}";
			var client = await GetClient();
			using (var response = await client.DeleteAsync(urlWithDate, cancellationToken))
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					LastError = responseBody;
					return default;
				}
				var deleteResponse = Deserialize<T>(responseBody);
				return deleteResponse;
			}
		}

		public string LastError { get; set; }

		public async Task<List<T>> GetAsync<T>(
			string operationPath, object o = null, HttpMethod overrideHttpMethod = null, bool stream = false, CancellationToken cancellationToken = default
		)
		{
			//var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			//var urlWithDate = $"{Service.BaseUrl}{operationPath}?date={date}";
			var urlWithDate = $"{Service.BaseUrl}{operationPath}";
			var client = await GetClient();
			client.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			HttpResponseMessage response;
			var completionOption = stream
				? HttpCompletionOption.ResponseHeadersRead
				: HttpCompletionOption.ResponseContentRead;
			var request = new HttpRequestMessage();
			if (o == null)
			{
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Method = overrideHttpMethod ?? HttpMethod.Get;
			}
			else if (overrideHttpMethod == HttpMethod.Get)
			{
				var parameters = ConvertToNameValueCollection(o, true);
				if (parameters.Count > 0)
					urlWithDate += "&" + parameters.ToString();
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Method = HttpMethod.Get;
			}
			else
			{
				var json = Serialize(o);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				request.Method = HttpMethod.Post;
				request.Content = content;
			}
			request.RequestUri = new Uri(urlWithDate);
			response = await client.SendAsync(request, completionOption, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				LastError = await response.Content.ReadAsStringAsync();
				return null;
			}
			//response.EnsureSuccessStatusCode();
			var list = new List<T>();
			if (stream)
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync())
				{
					using (var streamReader = new StreamReader(responseStream, Encoding.UTF8))
					{
						string line;
						while ((line = await streamReader.ReadLineAsync()) != null)
						{
							if (line.Contains("[DONE]"))
								break;
							var dataStartIndex = line.IndexOf("{");
							if (dataStartIndex < 0)
								continue;
							var jsonLine = line.Substring(dataStartIndex);
							var responseObject = Deserialize<T>(jsonLine);
							list.Add(responseObject);
						}
					}
				}
			}
			else
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				var responseObject = Deserialize<T>(responseBody);
				list.Add(responseObject);
			}
			return list;
		}

		/// <summary>
		/// Get Data from API with the spinner busy indicator.
		/// </summary>
		public async Task<List<T>> GetAsyncWithTask<T>(string path, object request = null, HttpMethod overrideHttpMethod = null)
		{
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			Global.MainControl.InfoPanel.AddTask(cancellationTokenSource);
			List<T> results = null;
			try
			{
				results = await GetAsync<T>(path, request, overrideHttpMethod, cancellationToken: cancellationTokenSource.Token);
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(cancellationTokenSource);
			}
			return results;
		}
		public async Task<deleted_response> DeleteFileAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(filesPath, id, cancellationToken);

		public async Task<deleted_response> DeleteFineTuningJobAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(fineTuningJobsPath, id, cancellationToken);

		public async Task<fine_tune> CancelFineTuningJobAsync(string id, CancellationToken cancellationToken = default)
		{
			var path = $"{fineTuningJobsPath}/{id}/cancel";
			var result = await GetAsync<fine_tune>(path, null, HttpMethod.Post, false, cancellationToken);
			return result?.FirstOrDefault();
		}

		public async Task<List<files>> GetFilesAsync()
			=> await GetAsyncWithTask<files>(filesPath);

		public async Task<fine_tune> CreateFineTuneJob(fine_tune_request r)
			=> (await GetAsyncWithTask<fine_tune>(fineTuningJobsPath, r))?.FirstOrDefault();

		public async Task<List<fine_tuning_jobs_response>> GetFineTuningJobsAsync(fine_tuning_jobs_request request)
		=> await GetAsyncWithTask<fine_tuning_jobs_response>(fineTuningJobsPath, request, HttpMethod.Get);

		public async Task<List<models_response>> GetModelsAsync()
			=> await GetAsyncWithTask<models_response>(modelsPath);

		public async Task<model[]> GetModels()
		{
			var response = await GetModelsAsync();
			return response?.FirstOrDefault()?.data
				.OrderBy(x => x.id.StartsWith("ft:") ? 0 : 1)
				.ThenBy(x => x.id)
				.ToArray() ?? Array.Empty<model>();
		}

		public async Task<deleted_response> DeleteModelAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(modelsPath, id, cancellationToken);

		public async Task<List<usage_response>> GetUsageAsync()
			=> await GetAsyncWithTask<usage_response>(usagePath);

		public event EventHandler MessageDone;

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
			foreach (var kv in item.RequestBodyData.Items.Where(x=>!string.IsNullOrEmpty(x.Key)))
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

		/// <summary>
		/// Get embedding vectors.
		/// </summary>
		/// <param name="modelName"></param>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task<OperationResult<Dictionary<int, float[]>>> GetEmbedding(
			string modelName,
			IEnumerable<string> input,
			CancellationToken cancellationToken = default
			)
		{
			var client = await GetAiClient(false);
			var clientToken = new CancellationTokenSource();
			clientToken.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(clientToken.Token, cancellationToken);
			var id = Guid.NewGuid();
			ControlsHelper.AppInvoke(() =>
			{
				//item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
			});
			Dictionary<int, float[]> results = null;
			try
			{
				var embeddingClient = client.GetEmbeddingClient(modelName);
				var response = await embeddingClient.GenerateEmbeddingsAsync(input, cancellationToken: linkedTokenSource.Token);
				if (response != null)
				{
					var inputTokens = response.Value.Usage.InputTokenCount;
					var totalTokens = response.Value.Usage.TotalTokenCount;
					results = response.Value
						.ToDictionary(x => x.Index, x => x.ToFloats().ToArray());
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<Dictionary<int, float[]>>(ex);
			}
			finally
			{
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
				});
			}
			return new OperationResult<Dictionary<int, float[]>>(results);
		}

		/// <summary>
		/// Query AI
		/// </summary>
		/// <param name="item">Item that will be affected: Used for insert/remove HttpClients.</param>
		public async Task<List<MessageItem>> QueryAI(
			TemplateItem item,
			List<chat_completion_message> messagesToSend,
			string embeddingText
		)
		{
			// Service item.
			var service = item.AiService;
			var modelName = item.AiModel;
			var aiModel = Global.AiModels.Items.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			var maxInputTokens = GetMaxInputTokens(item);
			// Other settings.
			var newMessageItems = new List<MessageItem>();
			var functionResults = new List<MessageAttachments>();
			var answer = "";
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(service.ResponseTimeout));
			var id = Guid.NewGuid();
			var assistantMessageItem = new MessageItem(ClientHelper.AiName, "", MessageType.In);
			ControlsHelper.AppInvoke(() =>
			{
				item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
				Global.AvatarPanel?.PlayMessageSentAnimation();
				newMessageItems.Add(assistantMessageItem);
				item.Messages.Add(assistantMessageItem);
				assistantMessageItem.Status = "Thinking";
			});
			var secure = new Uri(service.BaseUrl).Scheme == Uri.UriSchemeHttps;
			try
			{
				var messages = new List<ChatMessage>();
				foreach (var messageToSend in messagesToSend)
				{
					var stringContent = messageToSend.content as string;
					ChatMessageContentPart[] contentItems = null;
					if (messageToSend.content is content_item[] citems)
						contentItems = citems.Select(x => ConvertToChatMessageContentItem(x)).ToArray();
					switch (messageToSend.role)
					{
						case message_role.user:
							if (contentItems != null)
								messages.Add(new UserChatMessage(contentItems));
							else if (!string.IsNullOrEmpty(stringContent))
								messages.Add(new UserChatMessage(stringContent));
							break;
						case message_role.assistant:
							if (!string.IsNullOrEmpty(stringContent))
								messages.Add(new AssistantChatMessage(stringContent));
							break;
						case message_role.system:
							if (!string.IsNullOrEmpty(stringContent))
								messages.Add(new SystemChatMessage(stringContent));
							break;
					}
				}

				var completionsOptions = GetChatCompletionOptions(item);
				var addToolsToOptions = item.PluginsEnabled && aiModel.HasFeature(AiModelFeatures.FunctionCalling);
				var addToolsToMessage = item.PluginsEnabled && !aiModel.HasFeature(AiModelFeatures.FunctionCalling);
				ControlsHelper.AppInvoke(() =>
				{
					if (addToolsToOptions)
					{
						var tools = PluginsManager.GetChatToolDefinitions(item);
						PluginsManager.ProvideTools(tools, item, options: completionsOptions);
					}
				});
				var client = await GetAiClient(true, item);
				var chatClient = client.GetChatClient(modelName);
				var toolCalls = new List<ChatToolCall>();
				// If streaming  mode is enabled and AI model supports streaming then...
				if (service.ResponseStreaming && aiModel.HasFeature(AiModelFeatures.Streaming))
				{
					var toolCallIdsByIndex = new Dictionary<int, string>();
					var functionNamesByIndex = new Dictionary<int, string>();
					var functionArgumentsByIndex = new Dictionary<int, MemoryStream>();

					var result = chatClient.CompleteChatStreamingAsync(
						messages, completionsOptions, cancellationTokenSource.Token);

					var choicesEnumerator = result.GetAsyncEnumerator(cancellationTokenSource.Token);
					// Loop through the enumerator asynchronously
					try
					{
						while (await choicesEnumerator.MoveNextAsync().ConfigureAwait(false))
						{
							var choice = choicesEnumerator.Current;
							if (choice.ContentUpdate != null)
							{
								foreach (var cu in choice.ContentUpdate)
								{
									answer += cu.Text;
									ControlsHelper.AppInvoke(() =>
									{
										if (assistantMessageItem.Status != null)
											assistantMessageItem.Status = null;
										assistantMessageItem.AddToBodyBuffer(cu.Text);
									});
								}
							}
							if (choice.ToolCallUpdates != null)
							{
								foreach (StreamingChatToolCallUpdate update in choice.ToolCallUpdates)
								{
									var index = update.Index;
									if (!string.IsNullOrEmpty(update.ToolCallId))
										toolCallIdsByIndex[index] = update.ToolCallId;
									if (!string.IsNullOrEmpty(update.FunctionName))
										functionNamesByIndex[index] = update.FunctionName;
									if (update.FunctionArgumentsUpdate != null)
									{
										// If arguments storage doesn't exist yet, then...
										if (!functionArgumentsByIndex.TryGetValue(index, out MemoryStream stream))
										{
											stream = new MemoryStream();
											functionArgumentsByIndex[index] = stream;
										}
										using (Stream updateStream = update.FunctionArgumentsUpdate.ToStream())
											updateStream.CopyTo(stream);
									}
								}
							}
							// Yield control to allow UI updates or other tasks to process
							await Task.Yield();
						}
					}
					finally
					{
						if (choicesEnumerator != null)
							await choicesEnumerator.DisposeAsync();
					}

					foreach (var kv in toolCallIdsByIndex)
					{
						var index = kv.Key;
						var toolCallId = kv.Value;
						var functionName = functionNamesByIndex[index];
						// Getting function arguments.
						var stream = functionArgumentsByIndex[index];
						stream.Position = 0; // Reset the stream position to the beginning
						var functionArguments = BinaryData.FromStream(stream);
						var toolCall = ChatToolCall.CreateFunctionToolCall(toolCallId, functionName, functionArguments);
						toolCalls.Add(toolCall);
					}
				}
				// Streaming is not supported.
				// Could be local non-secure HTTP connection.
				else
				{
					var result = await chatClient.CompleteChatAsync(
						messages, completionsOptions, cancellationTokenSource.Token);
					var completion = result.Value;
					switch (completion.FinishReason)
					{
						case ChatFinishReason.Stop:
						case ChatFinishReason.ToolCalls:
							answer = string.Join("\r\n", completion.Content?.Select(x => x.Text));
							break;
						case ChatFinishReason.Length:
							answer = "Incomplete model output due to MaxTokens parameter or token limit exceeded.";
							break;
						case ChatFinishReason.ContentFilter:
							answer = "Omitted content due to a content filter flag.";
							break;
						default:
							answer = result.ToString();
							break;
					}
					if (completion.ToolCalls?.Any() == true)
						toolCalls.AddRange(completion.ToolCalls);
				}
				List<chat_completion_function> functions = null;
				if (addToolsToMessage)
				{
					// Get new answer message without function JSON and function calls.
					var (assistantMessage, functionCalls) = PluginsManager.ProcessAssistantMessage(answer);
					if (functionCalls.Any())
					{
						answer = assistantMessage;
						functions = functionCalls.ToList();
					}
				}
				if (addToolsToOptions)
				{
					functions = ConvertChatToolCallsTo(toolCalls);
				}
				// Get approval and process functions.
				await ProcessFunctions(item, functions,
					functionResults, assistantMessageItem,
					cancellationTokenSource);
			}
			catch (Exception ex)
			{
				// Preserves the original stack trace as if the exception was never caught and rethrow.
				// Allows parent catch see the exception exactly as it was at the original throw point.
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
			finally
			{
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
					item.CancellationTokenSources.Remove(cancellationTokenSource);
					Global.AvatarPanel?.PlayMessageReceivedAnimation();
				});
				MessageDone?.Invoke(this, EventArgs.Empty);
			}
			if (assistantMessageItem.Body != answer)
				assistantMessageItem.Body = answer;
			assistantMessageItem.Date = DateTime.Now;
			ControlsHelper.AppInvoke(() =>
			{
				assistantMessageItem.Updated = DateTime.Now;
				assistantMessageItem.Status = null;
			});
			if (!cancellationTokenSource.IsCancellationRequested && functionResults.Any())
			{
				var userAutoReplyMessageItem = new MessageItem(ClientHelper.UserName, "", MessageType.Out);
				foreach (var functionResult in functionResults)
					userAutoReplyMessageItem.Attachments.Add(functionResult);
				userAutoReplyMessageItem.IsAutomated = true;
				ControlsHelper.AppInvoke(() =>
				{
					newMessageItems.Add(userAutoReplyMessageItem);
					item.Messages.Add(userAutoReplyMessageItem);
				});
			}
			return newMessageItems;
		}

		public static ChatCompletionOptions GetChatCompletionOptions(TemplateItem item)
		{
			var options = new ChatCompletionOptions();
			// If creativity not normal then add it.
			if (item.Creativity != 1)
				options.Temperature = (float)item.Creativity;
			if (item.MaxCompletionTokensEnabled)
				options.MaxOutputTokenCount = item.MaxCompletionTokens;
			//// Need to use reflection to set the Temperature property
			//// because the developers used unnecessary C# 9.0 features that won't work on .NET 4.8.
			//typeof(ChatCompletionOptions)
			//	.GetProperty(nameof(ChatCompletionOptions.Temperature), BindingFlags.Public | BindingFlags.Instance)
			//		?.SetValue(options, (float)item.Creativity, null);
			return options;
		}


		/// <summary>
		/// Get function definitions that will be serialized and provided to the AI.
		/// </summary>
		public static List<chat_completion_function> ConvertChatToolsTo(IReadOnlyList<ChatTool> toolCalls)
		{
			var functions = new List<chat_completion_function>();
			if (toolCalls?.Any() != true)
				return functions;
			foreach (var toolCall in toolCalls)
			{
				var json = JsonSerializer.Serialize(toolCall);
				var parameters = new base_item();
				if (toolCall.FunctionParameters != null)
					parameters = JsonSerializer.Deserialize<base_item>(toolCall.FunctionParameters.ToString());
				var function = new chat_completion_function()
				{
					name = toolCall.FunctionName,
					description = toolCall.FunctionDescription,
					parameters = parameters,
				};
				functions.Add(function);
			}
			return functions;
		}

		/// <summary>
		/// Convert function calls returned by the assitant to completion functions.
		/// </summary>
		public static List<chat_completion_function> ConvertChatToolCallsTo(IReadOnlyList<ChatToolCall> toolCalls)
		{
			var functions = new List<chat_completion_function>();
			if (toolCalls?.Any() != true)
				return functions;
			foreach (var toolCall in toolCalls)
			{
				var json = JsonSerializer.Serialize(toolCall);
				var parameters = new base_item();
				if (toolCall.FunctionArguments != null)
					parameters = JsonSerializer.Deserialize<base_item>(toolCall.FunctionArguments);
				var function = new chat_completion_function()
				{
					id = toolCall.Id,
					name = toolCall.FunctionName,
					parameters = parameters,
				};
				functions.Add(function);
			}
			return functions;
		}

		public static async Task ProcessFunctions(
			TemplateItem item,
			List<chat_completion_function> functions,
			// Output parameters
			List<MessageAttachments> functionResults,
			MessageItem assistantMessageItem,
			CancellationTokenSource cancellationTokenSource
			)
		{
			if (functions?.Any() != true)
				return;
			var functionsList = functions.Select(f => new
			{
				f.id,
				f.name,
				parameters = PluginsManager.ConvertFromToolItem(PluginsManager.GetPluginFunctions().FirstOrDefault(x => x.Name == f.name)?.Mi, f)
			});

			// Serialize function calls as YAML for display as attachment to avoid confusing the AI.
			// Otherwise, it starts outputting JSON instead of calling functions.
			//var yaml = new SerializerBuilder().Build().Serialize(functionsList);
			var json = Serialize(functionsList, true);
			// Create message attachment first.
			//var fnCallAttachment = new MessageAttachments(ContextType.None, "YAML", yaml);
			var fnCallAttachment = new MessageAttachments(ContextType.None, "JSON", json);
			fnCallAttachment.Title = "Function Calls";
			// Don't send it back to AI or it will confuse it and it will start outputing YAML instead of calling functions.
			fnCallAttachment.SendType = AttachmentSendType.User;
			// Note: Maybe ask AI asistant to record call in its reply.
			// Add call to user message so that AI will see what functions it called.
			//var fnCallAttachmentUser = new MessageAttachments(ContextType.None, "YAML", yaml);
			//var fnCallAttachmentUser = new MessageAttachments(ContextType.None, "JSON", json);
			//fnCallAttachmentUser.Title = "Functions Call";
			//fnCallAttachmentUser.SendType = AttachmentSendType.User;
			//functionResults.Add(fnCallAttachmentUser);
			ControlsHelper.AppInvoke(() =>
			{
				assistantMessageItem.Attachments.Add(fnCallAttachment);
				assistantMessageItem.IsAutomated = true;
				var now = DateTime.Now;
				assistantMessageItem.Updated = now;
				item.Modified = now;
			});
			// Process function calls.
			if (item.PluginsEnabled)
			{
				foreach (var function in functions)
				{
					var content = await PluginsManager.ProcessPluginFunction(item, function, cancellationTokenSource);
					var fnResultAttachment = new MessageAttachments(ContextType.None, content.Value.Item1, content.Value.Item2);
					fnResultAttachment.Title = "Function Results (Id:" + function.id + ")";
					fnResultAttachment.SendType = AttachmentSendType.None;
					functionResults.Add(fnResultAttachment);
				}
			}
		}

		public static int GetMaxInputTokens(TemplateItem item)
		{
			var modelName = item.AiModel;
			// Try to get max input tokens value from the settings.
			var aiModel = Global.AiModels.Items.FirstOrDefault(x =>
				x.AiServiceId == item.AiServiceId && x.Name == item.AiModel);
			if (aiModel != null && aiModel.MaxInputTokens != 0)
				return aiModel.MaxInputTokens;
			return GetMaxInputTokens(modelName);
		}

		public static int GetMaxInputTokens(string modelName)
		{
			// Autodetect.
			modelName = modelName.ToLowerInvariant();
			// Final o1 and o3 supports 200K tokens.
			if ((modelName.StartsWith("o1") || modelName.StartsWith("o3"))
				&& !modelName.Contains("o1-preview"))
				return 200 * 1000;
			// All GPT-4 preview models support 128K tokens (2024-01-28).
			if (modelName.Contains("-128k") ||
				modelName.StartsWith("o1") ||
				modelName.Contains("gpt-4o") ||
				modelName.Contains("grok") ||
				modelName.Contains("gemini") ||
				(modelName.Contains("gpt-4") && modelName.Contains("preview")))
				return 128 * 1000;
			if (modelName.Contains("-64k") || modelName.Contains("deepseek"))
				return 64 * 1024;
			if (modelName.Contains("-32k") || modelName.Contains("text-moderation"))
				return 32 * 1024;
			if (modelName.Contains("-16k") || modelName.Contains("gpt-3.5-turbo-1106"))
				return 16 * 1024;
			if (modelName.Contains("gpt-4") || modelName.Contains("text-embedding"))
				return 8192;
			if (modelName.Contains("gpt-3.5-turbo"))
				return 4096;
			if (modelName.Contains("gpt"))
				return 4097; // Default for gpt
			if (modelName.Contains("code-davinci-002"))
				return 8001;
			if (modelName.Contains("text-davinci-002") || modelName.Contains("text-davinci-003"))
				return 4097;
			if (modelName.Contains("ada") || modelName.Contains("babbage") || modelName.Contains("curie") || modelName.Contains("davinci"))
				return 2049; // Default for ada, babbage, curie, davinci
			if (modelName.Contains("code-cushman-001"))
				return 2048;
			return 128 * 1000; // Default for other models
		}

		public static void SetModelFeatures(AiModel item)
		{
			if (item.Name.StartsWith("o1-preview"))
			{
				item.Features = AiModelFeatures.ChatSupport;
				item.IsFeaturesKnown = true;
			}
			else if (item.Name.StartsWith("o1"))
			{
				item.Features = AiModelFeatures.ChatSupport | AiModelFeatures.SystemMessages | AiModelFeatures.FunctionCalling;
				item.IsFeaturesKnown = true;
				item.Instructions = "Reply using a markdown code block for any code included.";
			}
			else if (item.Name.Contains("gemini"))
			{
				item.Features = AiModelFeatures.ChatSupport | AiModelFeatures.SystemMessages;
				item.IsFeaturesKnown = true;
			}
		}

		#region Convert to Name Value Collection

		public ChatMessageContentPart ConvertToChatMessageContentItem(object o)
		{
			if (!(o is content_item item))
				return null;
			switch (item.type)
			{
				case cotent_item_type.text:
					return ChatMessageContentPart.CreateTextPart(item.text);
				case cotent_item_type.image_url:
					// The Microsoft Uri has a size limit of x0FFF0.
					// At the moment the ChatMessageImageUrl does not support attaching base64 images larger than that.
					var detail = (ChatImageDetailLevel)item.image_url.detail.ToString();
					ChatMessageContentPart ci = null;
					if (ClassLibrary.Files.Mime.TryParseDataUri(item.image_url.url, out string mimeType, out byte[] data))
					{
						var bytes = BinaryData.FromBytes(data);
						ci = ChatMessageContentPart.CreateImagePart(bytes, mimeType, detail);
					}
					else
					{
						var imageUri = new System.Uri(item.image_url.url);
						ci = ChatMessageContentPart.CreateImagePart(imageUri, detail);
					}
					return ci;
				default:
					return null;
			}
		}

		public NameValueCollection ConvertToNameValueCollection(object o, bool escapeForUrl = false)
		{
			var collection = HttpUtility.ParseQueryString(string.Empty);
			var props = o.GetType().GetProperties();
			// Get all properties of the object
			foreach (var prop in props)
			{
				// Get property value
				var value = prop.GetValue(o);
				// If value is default for its type, skip serialization
				if (value == null || value.Equals(GetDefault(prop.PropertyType)))
					continue;
				// Convert property value to Json string
				var jsonValue = System.Text.Json.JsonSerializer.Serialize(value);
				// If escapeForUrl flag is set, URL encode the name and value
				var key = escapeForUrl ? Uri.EscapeDataString(prop.Name) : prop.Name;
				var val = escapeForUrl ? Uri.EscapeDataString(jsonValue) : jsonValue;
				// Add property name and value to the collection
				collection[key] = val;
			}
			return collection;
		}


		private static ConcurrentDictionary<Type, object> _defaultValuesCache = new ConcurrentDictionary<Type, object>();

		private static object GetDefault(Type type)
		{
			return _defaultValuesCache.GetOrAdd(type, t => (t.IsValueType ? Activator.CreateInstance(t) : null));
		}

		#endregion

	}

}
