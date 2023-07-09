using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System;
using System.Net.Http.Headers;
using OpenAI;
using System.Linq;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class Client : IClient
	{
		public Client(string baseUrl)
		{
			BaseUrl = baseUrl;
		}
		private string BaseUrl;
		private const string usageUrl = "/usage";
		private const string modelsUrl = "/models";
		public static Settings Settings;

		public HttpClient GetClient()
		{
			var client = new HttpClient();
			client.BaseAddress = new Uri(BaseUrl);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiSecretKey);
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			client.DefaultRequestHeaders.Add("OpenAI-Organization", Settings.ApiOrganizationId);
			return client;
		}

		/// <summary>
		/// Get usage.
		/// </summary>
		/// <returns></returns>
		public async Task<string> GetResponseAsync(string url)
		{
			var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var urlWithDate = $"{BaseUrl}/{url}?date={date}";
			var client = GetClient();
			var response = await client.GetAsync(urlWithDate);
			var responseBody = await response.Content.ReadAsStringAsync();
			//var usage = await response.Content.ReadFromJsonAsync<Usage>();
			Console.WriteLine(responseBody); // Print the response content for debugging purposes
			response.EnsureSuccessStatusCode();
			response.Dispose();
			return responseBody;
		}

		public async Task<OpenAI.Usage> GetUsageAsync()
		{
			var id = Guid.NewGuid();
			try
			{
				Global.MainControl.InfoPanel.AddTask(id);
				var responseBody = await GetResponseAsync(usageUrl);
				var o = JsonSerializer.Deserialize<OpenAI.Usage>(responseBody);
				return o;
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				return new Usage();
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
			}
		}

		public async Task<Model[]> GetModels()
		{
			var id = Guid.NewGuid();
			try
			{
				Global.MainControl.InfoPanel.AddTask(id);
				var apiClient = new OpenAI.ApiClient(GetClient());
				var models = await apiClient.ListModelsAsync();
				var list = models.Data.ToArray();
				return list;
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				return new Model[0];
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
			}
		}

		public async Task<string> QueryAI(
			string modelName,
			string prompt, string chatLog,
			List<ChatCompletionRequestMessage> messagesToSend,
			double creativity)
		{
			string answer;
			var id = Guid.NewGuid();
			Global.MainControl.InfoPanel.AddTask(id);
			try
			{
				var apiClient = new ApiClient(GetClient());
				if (modelName.Contains("davinci"))
				{
					var request = new CreateCompletionRequest
					{
						Model = modelName,
						Prompt = prompt + chatLog,
						// Comment out the Max_tokens line to allow the AI to use the maximum available amount.
						// Max_tokens = GetMaxTokens(modelName) - CountTokens(prompt + chatLog),
						N = 1,
						Stop = null,
						Temperature = creativity,
						Top_p = 1.0,
					};
					var result = await apiClient.CreateCompletionAsync(request);
					answer = result.Choices.FirstOrDefault()?.Text.Trim();
				}
				else
				{
					if (messagesToSend.Count == 0)
					{
						messagesToSend = new List<ChatCompletionRequestMessage> {
							new ChatCompletionRequestMessage()
							{
								Content = prompt,
								Role =  ChatCompletionRequestMessageRole.user,
							}
						};
					}
					var request = new CreateChatCompletionRequest
					{
						Model = modelName,
						Messages = messagesToSend,
						// Comment out the Max_tokens line to allow the AI to use the maximum available amount.
						// Max_tokens = GetMaxTokens(modelName) - CountTokens(prompt),
						N = 1,
						Stop = null,
						Temperature = creativity,
						Top_p = 1.0,
					};
					var result = await apiClient.CreateChatCompletionAsync(request);
					answer = result.Choices.FirstOrDefault()?.Message.Content.Trim();
				}
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
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
