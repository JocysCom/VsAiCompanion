using Azure.AI.OpenAI;
using Azure.Identity;
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
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using YamlDotNet.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class Client
	{
		public Client(AiService service)
		{
			Service = service;
		}
		private const string usagePath = "usage";
		private const string modelsPath = "models";
		private const string filesPath = "files";
		private const string chatCompletionsPath = "chat/completions";
		private const string completionsPath = "completions";
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
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{Service.BaseUrl}{filesPath}?date={date}";
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
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{Service.BaseUrl}{path}/{id}?date={date}";
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

		public string LastError;

		public async Task<List<T>> GetAsync<T>(
			string operationPath, object o = null, HttpMethod overrideHttpMethod = null, bool stream = false, CancellationToken cancellationToken = default
		)
		{
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{Service.BaseUrl}{operationPath}?date={date}";
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

		public async Task<OpenAIClient> GetAiClient(CancellationToken cancellationToken = default)
		{
			// https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme?view=azure-dotnet-preview
			// https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI/src
			var endpoint = new Uri(Service.BaseUrl);
			OpenAIClient client;
			var apiSecretKey = await Security.MicrosoftResourceManager.Current.GetKeyVaultSecretValue(Service.ApiSecretKeyVaultItemId, Service.ApiSecretKey);
			if (Service.IsAzureOpenAI)
			{
				client = string.IsNullOrEmpty(apiSecretKey)
					? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
					: new AzureOpenAIClient(endpoint, new System.ClientModel.ApiKeyCredential(apiSecretKey));
			}
			else
			{
				var credential = new System.ClientModel.ApiKeyCredential(apiSecretKey);
				// Create HttpClient with HttpClientLogger handler
				var logger = new HttpClientLogger(new HttpClientHandler());
				// Create the HttpClient to use HttpClientLogger
				var httpClient = new HttpClient(logger)
				{
					BaseAddress = endpoint,
					Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout),
				};
				// Register the handler in the HttpPipeline (hypothetical approach)
				var transport = new HttpClientPipelineTransport(httpClient);
				//var pipeline = new HttpPipeline(transport);
				var options = new OpenAIClientOptions();
				options.NetworkTimeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
				options.Transport = transport;
				options.Endpoint = endpoint;
				client = new OpenAIClient(credential, options);
				//var prop = client.GetType().GetField("_isConfiguredForAzureOpenAI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				//prop.SetValue(client, false);
			}
			return client;
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
			var client = await GetAiClient();
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
			TemplateItem serviceItem,
			List<chat_completion_message> messagesToSend,
			string embeddingText
		)
		{
			// Service item.
			var service = serviceItem.AiService;
			var modelName = serviceItem.AiModel;
			var aiModel = Global.AppSettings.AiModels.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			var creativity = serviceItem.Creativity;
			var maxInputTokens = GetMaxInputTokens(serviceItem);
			// Other settings.
			var messageItems = new List<MessageItem>();
			var assistantMessageItem = new MessageItem(ClientHelper.AiName, "", MessageType.In);
			var functionResults = new List<MessageAttachments>();
			var answer = "";
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(service.ResponseTimeout));
			var id = Guid.NewGuid();
			ControlsHelper.AppInvoke(() =>
			{
				serviceItem.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
				Global.AvatarPanel?.PlayMessageSentAnimation();
			});
			if (serviceItem.UseEmbeddings)
			{
				// Experimental.
				// AI must decide what to search for, not to use by the last user message.
				var embeddingItem = Global.Embeddings.Items
					.FirstOrDefault(x => x.Name == serviceItem.EmbeddingName);
				if (embeddingItem != null)
				{
					var eh = new EmbeddingHelper();
					var lastUserMessage = messagesToSend.Last(x => x.role == message_role.user);
					if (!string.IsNullOrWhiteSpace(lastUserMessage?.content as string))
					{
						// Try to get system message before user message.
						var lastSystemMessage = messagesToSend.LastOrDefault(x => x.role == message_role.system);
						var lastUserMessageIndex = messagesToSend.IndexOf(lastUserMessage);
						// If no system message
						var addNewSystemMessage = false;
						if (lastSystemMessage == null)
							addNewSystemMessage = true;
						// system message is not before user message, add one.
						else if ((messagesToSend.IndexOf(lastSystemMessage) - 1) != lastUserMessageIndex)
							addNewSystemMessage = true;
						if (addNewSystemMessage)
						{
							// Insert system message before user message.
							lastSystemMessage = new chat_completion_message(message_role.system, "");
							messagesToSend.Insert(lastUserMessageIndex, lastSystemMessage);
						}
						if (!string.IsNullOrEmpty(embeddingText))
						{
							var systemMessage = await eh.SearchEmbeddingsToSystemMessage(embeddingItem,
								serviceItem.EmbeddingGroupName, serviceItem.EmbeddingGroupFlag,
								embeddingText, embeddingItem.Skip, embeddingItem.Take);
							if (!string.IsNullOrEmpty(systemMessage))
								lastSystemMessage.content += "\r\n\r\n" + systemMessage;
						}
					}
				}
			}
			var secure = new Uri(service.BaseUrl).Scheme == Uri.UriSchemeHttps;
			try
			{
				// If Text Completion mode.
				if (IsTextCompletionMode(modelName))
				{
					var messages = messagesToSend
						.Select(x => new UserChatMessage(x.content as string))
						.ToList();
					var request = new text_completion_request
					{
						model = modelName,
						prompt = ClientHelper.JoinMessageParts(messagesToSend.Select(x => x.content as string).ToArray()),
						temperature = (float)creativity,
						stream = service.ResponseStreaming,
						max_tokens = maxInputTokens,

					};
					var data = await GetAsync<text_completion_response>(completionsPath, request, null, service.ResponseStreaming, cancellationTokenSource.Token);
					foreach (var dataItem in data)
						foreach (var chatChoice in dataItem.choices)
							answer += chatChoice.text;
				}
				// If Chat Completion mode.
				else
				{
					// If Azure service or HTTPS.
					if (service.IsAzureOpenAI || secure)
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

						var completionsOptions = GetChatCompletionOptions((float)creativity);
						var addToolsToOptions = serviceItem.PluginsEnabled && aiModel.HasFeature(AiModelFeatures.FunctionCalling);
						var addToolsToMessage = serviceItem.PluginsEnabled && !aiModel.HasFeature(AiModelFeatures.FunctionCalling);
						ControlsHelper.AppInvoke(() =>
						{
							if (addToolsToOptions)
							{
								var tools = PluginsManager.GetChatToolDefinitions(serviceItem);
								PluginsManager.ProvideTools(tools, serviceItem, options: completionsOptions);
							}
						});
						var client = await GetAiClient();
						var chatClient = client.GetChatClient(modelName);
						var toolCalls = new List<ChatToolCall>();
						// If streaming  mode is enabled and AI model supports streaming then...
						if (service.ResponseStreaming && aiModel.HasFeature(AiModelFeatures.Streaming))
						{
							var result = chatClient.CompleteChatStreamingAsync(
							messages, completionsOptions, cancellationTokenSource.Token);
							var choicesEnumerator = result.GetAsyncEnumerator(cancellationTokenSource.Token);

							var toolCallIdsByIndex = new Dictionary<int, string>();
							var functionNamesByIndex = new Dictionary<int, string>();
							var functionArgumentsByIndex = new Dictionary<int, MemoryStream>();

							while (await choicesEnumerator.MoveNextAsync())
							{
								var choice = choicesEnumerator.Current;
								if (choice.ContentUpdate != null)
								{
									foreach (var cu in choice.ContentUpdate)
										answer += cu.Text;
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
											// If arguments storage don't exists yet then...
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
						await ProcessFunctions(serviceItem, functions,
							functionResults, messageItems, assistantMessageItem,
							cancellationTokenSource);
					}
					else
					{
						var request = new chat_completion_request
						{
							model = modelName,
							temperature = (float)creativity,
							stream = service.ResponseStreaming,
							max_tokens = maxInputTokens,
						};
						request.messages = new List<chat_completion_message>();
						request.messages.AddRange(messagesToSend);
						ControlsHelper.AppInvoke(() =>
						{
							if (serviceItem.PluginsEnabled)
								PluginsManager.ProvideTools(serviceItem, request);
						});
						var data = await GetAsync<chat_completion_response>(chatCompletionsPath, request, null, service.ResponseStreaming, cancellationTokenSource.Token);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
							{
								var responseMessage = chatChoice.message;
								answer += (responseMessage ?? chatChoice.delta).content;
								ControlsHelper.AppInvoke(() =>
								{
									// Check if the model wanted to call a function
									if (serviceItem.PluginsEnabled)
										PluginsManager.ProcessPlugins(serviceItem, responseMessage);
								});
							}
					}
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
					serviceItem.CancellationTokenSources.Remove(cancellationTokenSource);
					Global.AvatarPanel?.PlayMessageReceivedAnimation();
				});
				MessageDone?.Invoke(this, EventArgs.Empty);
			}
			assistantMessageItem.Body = answer;
			assistantMessageItem.Date = DateTime.Now;
			if (!messageItems.Contains(assistantMessageItem))
				messageItems.Add(assistantMessageItem);
			if (!cancellationTokenSource.IsCancellationRequested && functionResults.Any())
			{
				var userAutoReplyMessageItem = new MessageItem(ClientHelper.UserName, "", MessageType.Out);
				foreach (var functionResult in functionResults)
					userAutoReplyMessageItem.Attachments.Add(functionResult);
				userAutoReplyMessageItem.IsAutomated = true;
				ControlsHelper.AppInvoke(() =>
				{
					messageItems.Add(userAutoReplyMessageItem);
				});
			}
			return messageItems;
		}

		public static ChatCompletionOptions GetChatCompletionOptions(float creativity)
		{
			var options = new ChatCompletionOptions();
			// Need to use reflection to set the Temperature property
			// because the developers used unnecessary C# 9.0 features that won't work on .NET 4.8.
			typeof(ChatCompletionOptions)
				.GetProperty(nameof(ChatCompletionOptions.Temperature), BindingFlags.Public | BindingFlags.Instance)
					?.SetValue(options, creativity, null);
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
			List<MessageItem> messageItems,
			MessageItem assistantMessageItem,
			CancellationTokenSource cancellationTokenSource
			)
		{
			if (functions?.Any() != true)
				return;
			// Serialize function calls as YAML for display as attachment to avoid confusing the AI.
			// Otherwise, it starts outputting JSON instead of calling functions.
			var serializer = new SerializerBuilder().Build();
			var yaml = serializer.Serialize(functions.Select(f => new
			{
				f.id,
				f.name,
				parameters = PluginsManager.ConvertFromToolItem(PluginsManager.GetPluginFunctions().FirstOrDefault(x => x.Name == f.name)?.Mi, f)
			}));
			// Create message attachment first.
			var fnCallAttachment = new MessageAttachments(ContextType.None, "YAML", yaml);
			fnCallAttachment.Title = "AI Functions Call";
			// Don't send it back to AI or it will confuse it and it will start outputing YAML instead of calling functions.
			fnCallAttachment.IsAlwaysIncluded = false;
			assistantMessageItem.Attachments.Add(fnCallAttachment);
			assistantMessageItem.IsAutomated = true;
			messageItems.Add(assistantMessageItem);
			// Add call to user message so that AI will see what functions it called.
			var fnCallAttachmentUser = new MessageAttachments(ContextType.None, "YAML", yaml);
			fnCallAttachmentUser.Title = "AI Functions Call";
			fnCallAttachmentUser.IsAlwaysIncluded = true;
			functionResults.Add(fnCallAttachmentUser);
			ControlsHelper.AppInvoke(() =>
			{
				item.Messages.Add(assistantMessageItem);
				item.Modified = DateTime.Now;
			});
			// Process function calls.
			if (item.PluginsEnabled)
			{
				foreach (var function in functions)
				{
					var content = await PluginsManager.ProcessPluginFunction(item, function, cancellationTokenSource);
					var fnResultAttachment = new MessageAttachments(ContextType.None, content.Value.Item1, content.Value.Item2);
					fnResultAttachment.Title = "AI Function Results (Id:" + function.id + ")";
					fnResultAttachment.IsAlwaysIncluded = true;
					functionResults.Add(fnResultAttachment);
				}
			}
		}

		public static bool IsTextCompletionMode(string modelName)
		{
			return modelName.Contains("davinci") || modelName.Contains("instruct");
		}

		public static int GetMaxInputTokens(TemplateItem item)
		{
			var modelName = item.AiModel;
			// Try to get max input tokens value from the settings.
			var aiModel = Global.AppSettings.AiModels.FirstOrDefault(x =>
				x.AiServiceId == item.AiServiceId && x.Name == item.AiModel);
			if (aiModel != null && aiModel.MaxInputTokens != 0)
				return aiModel.MaxInputTokens;
			return GetMaxInputTokens(modelName);
		}

		public static int GetMaxInputTokens(string modelName)
		{
			// Autodetect.
			modelName = modelName.ToLowerInvariant();
			if (modelName.Contains("gpt-5"))
				return 256 * 1000;
			// All GPT-4 preview models support 128K tokens (2024-01-28).
			if (modelName.Contains("-128k") ||
				modelName.StartsWith("o1") ||
				modelName.Contains("gpt-4o") ||
				(modelName.Contains("gpt-4") && modelName.Contains("preview")))
				return 128 * 1000;
			if (modelName.Contains("-64k"))
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
			return 2049; // Default for other models
		}

		public static void SetModelFeatures(AiModel item)
		{
			if (item.Name.StartsWith("o1"))
			{
				item.Features = AiModelFeatures.ChatSupport;
				item.IsFeaturesKnown = true;
				return;
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
