using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;
using Azure.Core;
using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;
using System.Text;
using System.Text.Json.Serialization;
using System.IO;
using System.Threading;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class Client
	{
		public Client(AiService service)
		{
			Service = service;
		}
		private const string usageUrl = "usage";
		private const string modelsUrl = "models";
		private const string chatCompletions = "chat/completions";
		private const string completions = "completions";
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

		public async Task<List<T>> GetAsync<T>(
			string operationPath, object o = null, bool stream = false, CancellationToken cancellationToken = default
		)
		{
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{Service.BaseUrl}{operationPath}?date={date}";
			var client = GetClient();
			client.Timeout = TimeSpan.FromSeconds(Service.ResponseTimeout);
			var options = new JsonSerializerOptions();
			options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
			options.Converters.Add(new UnixTimestampConverter());
			options.Converters.Add(new JsonStringEnumConverter());
			HttpResponseMessage response;
			var completionOption = stream
				? HttpCompletionOption.ResponseHeadersRead
				: HttpCompletionOption.ResponseContentRead;
			var request = new HttpRequestMessage();
			request.RequestUri = new Uri(urlWithDate);
			if (o == null)
			{
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Method = HttpMethod.Get;
				response = await client.SendAsync(request, completionOption, cancellationToken);
			}
			else
			{
				var json = JsonSerializer.Serialize(o, options);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				request.Method = HttpMethod.Post;
				request.Content = content;
				response = await client.SendAsync(request, completionOption, cancellationToken);
			}
			response.EnsureSuccessStatusCode();
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
							var responseObject = JsonSerializer.Deserialize<T>(jsonLine, options);
							list.Add(responseObject);
						}
					}
				}
			}
			else
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				var responseObject = JsonSerializer.Deserialize<T>(responseBody, options);
				list.Add(responseObject);
			}
			return list;
		}

		public async Task<List<usage_response>> GetUsageAsync()
		{
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			Global.MainControl.InfoPanel.AddTask(cancellationTokenSource);
			List<usage_response> results = null;
			try
			{
				results = await GetAsync<usage_response>(usageUrl, cancellationToken: cancellationTokenSource.Token);
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

		public async Task<List<models_response>> GetModelsAsync()
		{
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			Global.MainControl.InfoPanel.AddTask(cancellationTokenSource);
			List<models_response> results = null;
			try
			{
				results = await GetAsync<models_response>(modelsUrl, cancellationToken: cancellationTokenSource.Token);
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
				client = string.IsNullOrEmpty(Service.ApiAccessKey)
					? new OpenAIClient(endpoint, new DefaultAzureCredential())
					: new OpenAIClient(endpoint, new AzureKeyCredential(Service.ApiSecretKey));
			}
			else
			{
				var accessToken = new AccessToken(Service.ApiSecretKey, DateTimeOffset.Now.AddDays(180));
				var credential = DelegatedTokenCredential.Create((x, y) => accessToken);
				if (string.IsNullOrEmpty(Service.ApiAccessKey))
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
		/// Query AI
		/// </summary>
		/// <param name="item">Item that will be affected: Used for insert/remove HttpClients.</param>
		public async Task<string> QueryAI(
				string modelName,
				string prompt, string chatLog,
				List<chat_completion_message> messagesToSend,
				double creativity,
				TemplateItem item
			)
		{
			var answer = "";
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			var id = Guid.NewGuid();
			item.CancellationTokenSources.Add(cancellationTokenSource);
			Global.MainControl.InfoPanel.AddTask(id);
			var secure = new Uri(Service.BaseUrl).Scheme == Uri.UriSchemeHttps;
			try
			{
				// If Text Completion mode.
				if (IsTextCompletionMode(modelName))
				{
					// If Azure service or HTTPS.
					if (Service.IsAzureOpenAI || secure)
					{
						var client = GetAiClient();
						var messages = new List<string>();
						messages.Add(prompt + chatLog);
						var completionsOptions = new CompletionsOptions(messages);
						completionsOptions.Temperature = (float)creativity;
						if (Service.ResponseStreaming)
						{
							var response = await client.GetCompletionsStreamingAsync(modelName, completionsOptions, cancellationTokenSource.Token);
							using (var streamingChatCompletions = response.Value)
							{
								var choicesEnumerator = streamingChatCompletions
									.GetChoicesStreaming(cancellationTokenSource.Token)
									.GetAsyncEnumerator(cancellationTokenSource.Token);
								while (await choicesEnumerator.MoveNextAsync())
								{
									var choice = choicesEnumerator.Current;
									var messagesEnumerator = choice
										.GetTextStreaming(cancellationTokenSource.Token)
										.GetAsyncEnumerator(cancellationTokenSource.Token);
									while (await messagesEnumerator.MoveNextAsync())
										answer += messagesEnumerator.Current;
								}
							}
						}
						else
						{
							var response = await client.GetCompletionsAsync(modelName, completionsOptions, cancellationTokenSource.Token);
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
							temperature = (float)creativity,
							prompt = prompt + chatLog,
							stream = Service.ResponseStreaming,
							max_tokens = GetMaxTokens(modelName),

						};
						var data = await GetAsync<text_completion_response>(completions, request, Service.ResponseStreaming, cancellationTokenSource.Token);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
								answer += chatChoice.text;
					}
				}
				// If Chat Completion mode.
				else
				{
					var messages = new List<ChatMessage>();
					if (messagesToSend.Count == 0)
					{
						messages.Add(new ChatMessage(ChatRole.User, prompt));
					}
					else
					{
						messages = messagesToSend
							.Select(x => new ChatMessage(new ChatRole(x.role.ToString()), x.content))
							.ToList();
					}
					// If Azure service or HTTPS.
					if (Service.IsAzureOpenAI || secure)
					{
						var chatCompletionsOptions = new ChatCompletionsOptions(messages);
						chatCompletionsOptions.Temperature = (float)creativity;
						if (Service.ResponseStreaming)
						{
							var client = GetAiClient();
							var response = await client.GetChatCompletionsStreamingAsync(modelName, chatCompletionsOptions, cancellationTokenSource.Token);
							using (var streamingChatCompletions = response.Value)
							{
								var choicesEnumerator = streamingChatCompletions
									.GetChoicesStreaming(cancellationTokenSource.Token)
									.GetAsyncEnumerator(cancellationTokenSource.Token);
								while (await choicesEnumerator.MoveNextAsync())
								{
									var choice = choicesEnumerator.Current;
									var messagesEnumerator = choice
										.GetMessageStreaming(cancellationTokenSource.Token)
										.GetAsyncEnumerator(cancellationTokenSource.Token);
									while (await messagesEnumerator.MoveNextAsync())
										answer += messagesEnumerator.Current.Content;
								}
							}
						}
						// Could be local non-secure HTTP connection.
						else
						{
							var client = GetAiClient();
							var response = await client.GetChatCompletionsAsync(modelName, chatCompletionsOptions, cancellationTokenSource.Token);
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
							max_tokens = GetMaxTokens(modelName),
						};
						request.messages = messages.Select(x => new chat_completion_message()
						{
							role = (message_role)Enum.Parse(typeof(message_role), x.Role.ToString()),
							content = x.Content,
							name = x.Name,
						}).ToList();
						var data = await GetAsync<chat_completion_response>(chatCompletions, request, Service.ResponseStreaming, cancellationTokenSource.Token);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
								answer += (chatChoice.message ?? chatChoice.delta).content;
					}
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
				item.CancellationTokenSources.Remove(cancellationTokenSource);
				MessageDone?.Invoke(this, EventArgs.Empty);
			}
			return answer.TrimStart();
		}

		public static bool IsTextCompletionMode(string modelName)
		{
			return modelName.Contains("davinci");
		}

		public static int GetMaxTokens(string modelName)
		{
			modelName = modelName.ToLowerInvariant();
			if (modelName.Contains("gpt-4-32k"))
				return 32768;
			if (modelName.Contains("gpt-4"))
				return 8192;
			if (modelName.Contains("gpt-3-16k") || modelName.Contains("gpt-16k"))
				return 16384;
			if (modelName.Contains("gpt-35-turbo"))
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

	}

}
