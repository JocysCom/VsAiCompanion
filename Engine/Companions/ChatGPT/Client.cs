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

		public async Task<List<T>> GetAsync<T>(string operationPath, object o = null, bool stream = false)
		{
			var id = Guid.NewGuid();
			try
			{
				Global.MainControl.InfoPanel.AddTask(id);
				var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
				var urlWithDate = $"{Service.BaseUrl}{operationPath}?date={date}";
				HttpClient client = GetClient();
				var options = new JsonSerializerOptions();
				options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
				options.Converters.Add(new UnixTimestampConverter());
				options.Converters.Add(new JsonStringEnumConverter());
				HttpResponseMessage response;
				if (o == null)
				{
					client.DefaultRequestHeaders.Accept.Clear();
					client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					response = await client.GetAsync(urlWithDate);
				}
				else
				{
					var json = JsonSerializer.Serialize(o, options);
					var content = new StringContent(json, Encoding.UTF8, "application/json");
					response = await client.PostAsync(urlWithDate, content);
				}
				response.EnsureSuccessStatusCode();
				var list = new List<T>();
				if (stream)
				{
					using (var responseStream = await response.Content.ReadAsStreamAsync())
					{
						using (var streamReader = new StreamReader(responseStream))
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
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				return default;
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
			}
		}

		public class part
		{
			public JsonElement data;
		}

		public async Task<List<usage_response>> GetUsageAsync() =>
			await GetAsync<usage_response>(usageUrl);

		public async Task<List<models_response>> GetModelsAsync() =>
			await GetAsync<models_response>(modelsUrl);

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
				List<ChatMessage> messagesToSend,
				double creativity,
				TemplateItem item
			)
		{
			var answer = "";
			var id = Guid.NewGuid();
			var httpClient = GetClient();
			item.HttpClients.Add(httpClient);
			Global.MainControl.InfoPanel.AddTask(id);
			var client = GetAiClient();
			var notSecure = new Uri(Service.BaseUrl).Scheme == Uri.UriSchemeHttp;
			try
			{
				if (modelName.Contains("davinci"))
				{
					var messages = new List<string>();
					messages.Add(prompt + chatLog);
					var completionsOptions = new CompletionsOptions(messages);
					completionsOptions.Temperature = (float)creativity;

					// If not secure then use simple service.
					if (notSecure)
					{
						var request = new text_completion_request
						{
							model = modelName,
							temperature = (float)creativity,
							prompt = prompt + chatLog,
							stream = Service.ResponseStreaming
						};
						var data = await GetAsync<text_completion_response>(completions, request, Service.ResponseStreaming);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
								answer += chatChoice.text;
					}
					else if (Service.ResponseStreaming)
					{

						var response = await client.GetCompletionsStreamingAsync(modelName, completionsOptions);
						using (var streamingChatCompletions = response.Value)
						{
							var choicesEnumerator = streamingChatCompletions.GetChoicesStreaming().GetAsyncEnumerator();
							while (await choicesEnumerator.MoveNextAsync())
							{
								var choice = choicesEnumerator.Current;
								var messagesEnumerator = choice.GetTextStreaming().GetAsyncEnumerator();
								while (await messagesEnumerator.MoveNextAsync())
									answer += messagesEnumerator.Current;
							}
						}
					}
					else
					{
						var response = await client.GetCompletionsAsync(modelName, completionsOptions);
						foreach (var choice in response.Value.Choices)
						{
							answer += choice.Text;
							// Pick first first answer.
							break;
						}
					}
				}
				else
				{
					var messages = new List<ChatMessage>();
					if (messagesToSend.Count == 0)
					{
						messages.Add(new ChatMessage(ChatRole.User, prompt) { Name = ClientHelper.UserName });
					}
					else
					{
						messages = messagesToSend
							.Select(x => new ChatMessage(x.Role.ToString(), x.Content) { Name = ClientHelper.UserName })
							.ToList();
					}
					var chatCompletionsOptions = new ChatCompletionsOptions(messages);
					chatCompletionsOptions.Temperature = (float)creativity;
					// If not secure then use simple service.
					if (notSecure)
					{
						var request = new chat_completion_request
						{
							model = modelName,
							temperature = (float)creativity,
							stream = Service.ResponseStreaming,
						};
						request.messages = messages.Select(x => new chat_completion_message()
						{
							role = (message_role)Enum.Parse(typeof(message_role), x.Role.ToString()),
							content = x.Content,
							name = x.Name,
						}).ToList();
						var data = await GetAsync<chat_completion_response>(chatCompletions, request, Service.ResponseStreaming);
						foreach (var dataItem in data)
							foreach (var chatChoice in dataItem.choices)
								answer += (chatChoice.message ?? chatChoice.delta).content;
					}
					else if (Service.ResponseStreaming)
					{
						var response = await client.GetChatCompletionsStreamingAsync(modelName, chatCompletionsOptions);
						using (var streamingChatCompletions = response.Value)
						{
							var choicesEnumerator = streamingChatCompletions.GetChoicesStreaming().GetAsyncEnumerator();
							while (await choicesEnumerator.MoveNextAsync())
							{
								var choice = choicesEnumerator.Current;
								var messagesEnumerator = choice.GetMessageStreaming().GetAsyncEnumerator();
								while (await messagesEnumerator.MoveNextAsync())
									answer += messagesEnumerator.Current.Content;
							}
						}
					}
					else
					{
						var response = await client.GetChatCompletionsAsync(modelName, chatCompletionsOptions);
						foreach (ChatChoice chatChoice in response.Value.Choices)
						{
							answer += chatChoice.Message.Content;
							// Pick first first answer.
							break;
						}
					}
				}
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
				item.HttpClients.Remove(httpClient);
				MessageDone?.Invoke(this, EventArgs.Empty);
			}
			return answer;
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

		//public class Usage
		//{
		//	public string _object { get; set; }
		//	public object[] data { get; set; }
		//	public object[] ft_data { get; set; }
		//	public object[] dalle_api_data { get; set; }
		//	public object[] whisper_api_data { get; set; }
		//	public float current_usage_usd { get; set; }
		//}

	}

}
