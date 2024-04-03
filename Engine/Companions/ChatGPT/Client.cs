using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class Client
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

		public HttpClient GetClient()
		{
			var client = new HttpClient();
			client.BaseAddress = new Uri(Service.BaseUrl);
			//if (!string.IsNullOrEmpty(Service.ApiSecretKey))
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Service.ApiSecretKey);
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			//if (!string.IsNullOrEmpty(Service.ApiOrganizationId))
			client.DefaultRequestHeaders.Add("OpenAI-Organization", Service.ApiOrganizationId);
			return client;
		}

		public static JsonSerializerOptions GetJsonOptions()
		{
			var o = new JsonSerializerOptions();
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

		public static string Serialize(object o)
			=> JsonSerializer.Serialize(o, JsonOptions);

		public async Task<file> UploadFileAsync(string filePath, string purpose, CancellationToken cancellationToken = default)
		{
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{Service.BaseUrl}{filesPath}?date={date}";
			var client = GetClient();
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
			var client = GetClient();
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
			var client = GetClient();
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

		public OpenAIClient GetAiClient()
		{
			// https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme?view=azure-dotnet-preview
			// https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI/src
			var endpoint = new Uri(Service.BaseUrl);
			var options = new OpenAIClientOptions();
			OpenAIClient client;
			if (Service.IsAzureOpenAI)
			{
				client = string.IsNullOrEmpty(Service.ApiSecretKey)
					? new OpenAIClient(endpoint, new DefaultAzureCredential())
					: new OpenAIClient(endpoint, new AzureKeyCredential(Service.ApiSecretKey));
			}
			else
			{
				var accessToken = new AccessToken(Service.ApiSecretKey, DateTimeOffset.Now.AddDays(180));
				var credential = DelegatedTokenCredential.Create((x, y) => accessToken);
				if (string.IsNullOrEmpty(Service.ApiSecretKey))
				{
					// TODO: Allow HTTP localhost connections.
					// Bearer token authentication is not permitted for non TLS protected (https) endpoints.
				}
				client = new OpenAIClient(endpoint, credential, options);
				var prop = client.GetType().GetField("_isConfiguredForAzureOpenAI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				prop.SetValue(client, false);
			}
			return client;
		}

		/// <summary>
		/// Get embedding vectors.
		/// </summary>
		/// <param name="modelName"></param>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task<Dictionary<int, float[]>> GetEmbedding(
			string modelName,
			IEnumerable<string> input,
			CancellationToken cancellationToken = default
			)
		{
			var client = GetAiClient();
			var clientToken = new CancellationTokenSource();
			clientToken.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(clientToken.Token, cancellationToken);
			var id = Guid.NewGuid();
			Global.MainControl.Dispatcher.Invoke(() =>
			{
				//item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
			});
			Dictionary<int, float[]> results = null;
			try
			{
				var options = new EmbeddingsOptions(modelName, input);
				var response = await client.GetEmbeddingsAsync(options, linkedTokenSource.Token);
				if (response != null)
				{
					var promptTokens = response.Value.Usage.PromptTokens;
					var totalTokens = response.Value.Usage.TotalTokens;
					results = response.Value.Data
						.ToDictionary(x => x.Index, x => x.Embedding.ToArray());
				}
			}
			catch (Exception ex)
			{
			}
			finally
			{
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
				});
			}
			return results;
		}

		/// <summary>
		/// Query AI
		/// </summary>
		/// <param name="item">Item that will be affected: Used for insert/remove HttpClients.</param>
		public async Task<List<MessageItem>> QueryAI(
			string modelName,
			List<chat_completion_message> messagesToSend,
			double creativity,
			TemplateItem item,
			int maxInputTokens
		)
		{
			var messageItems = new List<MessageItem>();
			var assistantMessageItem = new MessageItem(ClientHelper.AiName, "", MessageType.In);
			var answer = "";
			var functionResults = new List<MessageAttachments>();
			var toolArgumentsUpdate = "";
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			var id = Guid.NewGuid();
			Global.MainControl.Dispatcher.Invoke(() =>
			{
				item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
			});
			if (item.UseEmbeddings)
			{
				// Experimental.
				// AI must decide what to search for, not to use by the last user message.
				var groupName = item.EmbeddingGroupName;
				var groupFlag = item.EmbeddingGroupFlag;
				var embeddingItem = Global.Embeddings.Items.FirstOrDefault(x => x.EmbeddingGroupFlag == groupFlag);
				if (embeddingItem != null)
				{
					var eh = new EmbeddingHelper();
					var lastUserMessage = messagesToSend.Last(x => x.role == message_role.user);
					if (!string.IsNullOrWhiteSpace(lastUserMessage?.content))
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
						var systemMessage = await eh.SearchEmbeddingsToSystemMessage(embeddingItem, lastUserMessage?.content, embeddingItem.Skip, embeddingItem.Take);
						if (!string.IsNullOrEmpty(systemMessage))
							lastSystemMessage.content += "\r\n\r\n" + systemMessage;

					}
				}
			}
			var secure = new Uri(Service.BaseUrl).Scheme == Uri.UriSchemeHttps;
			try
			{
				// If Text Completion mode.
				if (IsTextCompletionMode(modelName))
				{
					var prompts = messagesToSend.Select(x => x.content).ToArray();
					// If Azure service or HTTPS.
					if (Service.IsAzureOpenAI || secure)
					{
						var client = GetAiClient();
						var completionsOptions = new CompletionsOptions(modelName, prompts);
						completionsOptions.Temperature = (float)creativity;
						if (Service.ResponseStreaming)
						{
							var response = await client.GetCompletionsStreamingAsync(completionsOptions, cancellationTokenSource.Token);
							using (var streamingChatCompletions = response)
							{
								var iae = streamingChatCompletions.EnumerateValues();
								var choicesEnumerator = iae.GetAsyncEnumerator(cancellationTokenSource.Token);
								while (await choicesEnumerator.MoveNextAsync())
								{
									var completions = choicesEnumerator.Current;
									foreach (var choice in completions.Choices)
										answer += choice.Text;
								}
							}
						}
						else
						{
							var response = await client.GetCompletionsAsync(completionsOptions, cancellationTokenSource.Token);
							foreach (var choice in response.Value.Choices)
							{
								answer += choice.Text;
								// Pick first first answer.
								break;
							}
						}
					}
					// Could be local non-secure HTTP connection.
					else
					{
						var request = new text_completion_request
						{
							model = modelName,
							prompt = ClientHelper.JoinMessageParts(prompts),
							temperature = (float)creativity,
							stream = Service.ResponseStreaming,
							max_tokens = maxInputTokens,

						};
						var data = await GetAsync<text_completion_response>(completionsPath, request, null, Service.ResponseStreaming, cancellationTokenSource.Token);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
								answer += chatChoice.text;
					}
				}
				// If Chat Completion mode.
				else
				{
					var messages = new List<ChatRequestMessage>();
					foreach (var messageToSend in messagesToSend)
					{
						switch (messageToSend.role)
						{
							case message_role.user:
								messages.Add(new ChatRequestUserMessage(messageToSend.content));
								break;
							case message_role.assistant:
								messages.Add(new ChatRequestAssistantMessage(messageToSend.content));
								break;
							case message_role.system:
								messages.Add(new ChatRequestSystemMessage(messageToSend.content));
								break;
						}
					}

					// If Azure service or HTTPS.
					if (Service.IsAzureOpenAI || secure)
					{
						var chatCompletionsOptions = new ChatCompletionsOptions(modelName, messages);
						Global.MainControl.Dispatcher.Invoke(() =>
						{
							if (item.PluginsEnabled)
								PluginsManager.ProvideTools(item, chatCompletionsOptions);
						});
						chatCompletionsOptions.Temperature = (float)creativity;
						if (Service.ResponseStreaming)
						{
							var client = GetAiClient();
							var response = await client.GetChatCompletionsStreamingAsync(chatCompletionsOptions, cancellationTokenSource.Token);
							using (var streamingChatCompletions = response)
							{
								var iae = streamingChatCompletions.EnumerateValues();
								var choicesEnumerator = iae.GetAsyncEnumerator(cancellationTokenSource.Token);
								while (await choicesEnumerator.MoveNextAsync())
								{
									var choice = choicesEnumerator.Current;
									answer += choice.ContentUpdate;
									if (choice.ToolCallUpdate != null)
									{
										if (choice.ToolCallUpdate is StreamingFunctionToolCallUpdate sf)
										{
											if (!string.IsNullOrEmpty(sf.Id))
											{
												if (!string.IsNullOrEmpty(toolArgumentsUpdate))
													toolArgumentsUpdate += "\r\n},\r\n";
												toolArgumentsUpdate += $"{{\r\n";
												toolArgumentsUpdate += $"\t\"id\": \"{sf.Id}\",\r\n";
												toolArgumentsUpdate += $"\t\"name\": \"{sf.Name}\",\r\n";
												toolArgumentsUpdate += $"\t\"parameters\": ";
											}
											toolArgumentsUpdate += sf.ArgumentsUpdate;
										}
									}
								}
								if (!string.IsNullOrEmpty(toolArgumentsUpdate))
								{
									var json = "[\r\n" + toolArgumentsUpdate + "\r\n}\r\n]";
									var functions = Deserialize<chat_completion_function[]>(json);
									// Create message attachment first.
									var attachment = new MessageAttachments(ContextType.None, "JSON", json);
									attachment.Title = "AI Function Call";
									attachment.IsAlwaysIncluded = true;
									assistantMessageItem.Attachments.Add(attachment);
									assistantMessageItem.IsAutomated = true;
									messageItems.Add(assistantMessageItem);
									Global.MainControl.Dispatcher.Invoke(() =>
									{
										item.Messages.Add(assistantMessageItem);
									});
									// Process function calls.
									if (item.PluginsEnabled)
									{
										foreach (var function in functions)
										{
											var functionResultContent = await PluginsManager.ProcessPlugins(item, function, cancellationTokenSource);
											var fnAttachment = new MessageAttachments(ContextType.None, "text", functionResultContent);
											fnAttachment.Title = "AI Function Results (Id:" + function.id + ")";
											fnAttachment.IsAlwaysIncluded = true;
											functionResults.Add(fnAttachment);
										}
									}
								}
							}
						}
						// Could be local non-secure HTTP connection.
						else
						{
							var client = GetAiClient();
							var response = await client.GetChatCompletionsAsync(chatCompletionsOptions, cancellationTokenSource.Token);
							foreach (ChatChoice chatChoice in response.Value.Choices)
							{
								answer += chatChoice.Message.Content;
								// Pick first first answer.
								break;
							}
						}
					}
					else
					{
						var request = new chat_completion_request
						{
							model = modelName,
							temperature = (float)creativity,
							stream = Service.ResponseStreaming,
							max_tokens = maxInputTokens,
						};
						request.messages = new List<chat_completion_message>();
						foreach (var message in messages)
						{
							chat_completion_message msg = null;
							if (message is ChatRequestUserMessage userMessage)
								msg = new chat_completion_message { role = message_role.user, content = userMessage.Content, name = userMessage.Name };
							else if (message is ChatRequestAssistantMessage assistantMessage)
								msg = new chat_completion_message { role = message_role.assistant, content = assistantMessage.Content, name = assistantMessage.Name };
							else if (message is ChatRequestSystemMessage systemMessage)
								msg = new chat_completion_message { role = message_role.system, content = systemMessage.Content, name = systemMessage.Name };
							if (msg != null)
								request.messages.Add(msg);

						}
						Global.MainControl.Dispatcher.Invoke(() =>
						{
							if (item.PluginsEnabled)
								PluginsManager.ProvideTools(item, request);
						});
						var data = await GetAsync<chat_completion_response>(chatCompletionsPath, request, null, Service.ResponseStreaming, cancellationTokenSource.Token);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
							{
								var responseMessage = chatChoice.message;
								answer += (responseMessage ?? chatChoice.delta).content;
								Global.MainControl.Dispatcher.Invoke(() =>
								{
									// Check if the model wanted to call a function
									if (item.PluginsEnabled)
										PluginsManager.ProcessPlugins(item, responseMessage);
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
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
					item.CancellationTokenSources.Remove(cancellationTokenSource);
				});
				MessageDone?.Invoke(this, EventArgs.Empty);
			}
			assistantMessageItem.Body = answer;
			if (!messageItems.Contains(assistantMessageItem))
				messageItems.Add(assistantMessageItem);
			if (!cancellationTokenSource.IsCancellationRequested && functionResults.Count > 0)
			{
				var userAutoReplyMessageItem = new MessageItem(ClientHelper.UserName, "", MessageType.Out);
				userAutoReplyMessageItem.Attachments.AddRange(functionResults);
				userAutoReplyMessageItem.IsAutomated = true;
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					messageItems.Add(userAutoReplyMessageItem);
				});
			}
			return messageItems;
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
			// Autodetect.
			modelName = modelName.ToLowerInvariant();
			// All GPT-4 preview models support 128K tokens (2024-01-28).
			if (modelName.Contains("-128k") || (modelName.Contains("gpt-4") && modelName.Contains("preview")))
				return 128 * 1024;
			if (modelName.Contains("-64k"))
				return 64 * 1024;
			if (modelName.Contains("-32k"))
				return 32 * 1024;
			if (modelName.Contains("-16k") || modelName.Contains("gpt-3.5-turbo-1106"))
				return 16 * 1024;
			if (modelName.Contains("gpt-4"))
				return 8192;
			if (modelName.Contains("gpt-3.5-turbo"))
				return 4096;
			if (modelName.Contains("gpt"))
				return 4097; // Default for gpt
			if (modelName.Contains("text-embedding-ada-002") && modelName.Contains("version 2"))
				return 8191;
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

		#region Convert to Name Value Collection

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
