using JocysCom.ClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// API operations for file management, model operations, and fine-tuning.
	/// </summary>
	public partial class Client
	{
		#region Generic API Operations

		/// <summary>
		/// Generic method for making API calls with support for streaming responses.
		/// </summary>
		/// <typeparam name="T">The type of response expected.</typeparam>
		/// <param name="operationPath">The API endpoint path.</param>
		/// <param name="o">Optional request object.</param>
		/// <param name="overrideHttpMethod">Override HTTP method if needed.</param>
		/// <param name="stream">Whether to use streaming response.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>List of response objects.</returns>
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
		/// <typeparam name="T">The type of response expected.</typeparam>
		/// <param name="path">API endpoint path.</param>
		/// <param name="request">Optional request object.</param>
		/// <param name="overrideHttpMethod">Override HTTP method if needed.</param>
		/// <returns>List of response objects.</returns>
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

		/// <summary>
		/// Generic delete operation for API resources.
		/// </summary>
		/// <typeparam name="T">The type of response expected.</typeparam>
		/// <param name="path">API endpoint path.</param>
		/// <param name="id">Resource ID to delete.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Delete response.</returns>
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

		#endregion

		#region File Operations

		/// <summary>
		/// Uploads a file to the OpenAI API for use with fine-tuning or other operations.
		/// </summary>
		/// <param name="filePath">Path to the file to upload.</param>
		/// <param name="purpose">Purpose for the file (e.g., "fine-tune").</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>File information response.</returns>
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

		/// <summary>
		/// Gets the list of files uploaded to the OpenAI API.
		/// </summary>
		/// <returns>List of files.</returns>
		public async Task<List<files>> GetFilesAsync()
			=> await GetAsyncWithTask<files>(filesPath);

		/// <summary>
		/// Deletes a file from the OpenAI API.
		/// </summary>
		/// <param name="id">File ID to delete.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Delete response.</returns>
		public async Task<deleted_response> DeleteFileAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(filesPath, id, cancellationToken);

		#endregion

		#region Model Operations

		/// <summary>
		/// Gets the list of available models from the OpenAI API.
		/// </summary>
		/// <returns>List of models response.</returns>
		public async Task<List<models_response>> GetModelsAsync()
			=> await GetAsyncWithTask<models_response>(modelsPath);

		/// <summary>
		/// Gets an array of available models, ordered with fine-tuned models first.
		/// </summary>
		/// <returns>Array of models.</returns>
		public async Task<model[]> GetModels()
		{
			var response = await GetModelsAsync();
			return response?.FirstOrDefault()?.data
				.OrderBy(x => x.id.StartsWith("ft:") ? 0 : 1)
				.ThenBy(x => x.id)
				.ToArray() ?? Array.Empty<model>();
		}

		/// <summary>
		/// Deletes a fine-tuned model.
		/// </summary>
		/// <param name="id">Model ID to delete.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Delete response.</returns>
		public async Task<deleted_response> DeleteModelAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(modelsPath, id, cancellationToken);

		#endregion

		#region Fine-Tuning Operations

		/// <summary>
		/// Creates a new fine-tuning job.
		/// </summary>
		/// <param name="r">Fine-tune request parameters.</param>
		/// <returns>Fine-tune job information.</returns>
		public async Task<fine_tune> CreateFineTuneJob(fine_tune_request r)
			=> (await GetAsyncWithTask<fine_tune>(fineTuningJobsPath, r))?.FirstOrDefault();

		/// <summary>
		/// Gets the list of fine-tuning jobs.
		/// </summary>
		/// <param name="request">Request parameters for filtering jobs.</param>
		/// <returns>List of fine-tuning jobs.</returns>
		public async Task<List<fine_tuning_jobs_response>> GetFineTuningJobsAsync(fine_tuning_jobs_request request)
		=> await GetAsyncWithTask<fine_tuning_jobs_response>(fineTuningJobsPath, request, HttpMethod.Get);

		/// <summary>
		/// Cancels a fine-tuning job.
		/// </summary>
		/// <param name="id">Fine-tuning job ID to cancel.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Updated fine-tune job information.</returns>
		public async Task<fine_tune> CancelFineTuningJobAsync(string id, CancellationToken cancellationToken = default)
		{
			var path = $"{fineTuningJobsPath}/{id}/cancel";
			var result = await GetAsync<fine_tune>(path, null, HttpMethod.Post, false, cancellationToken);
			return result?.FirstOrDefault();
		}

		/// <summary>
		/// Deletes a fine-tuning job.
		/// </summary>
		/// <param name="id">Fine-tuning job ID to delete.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Delete response.</returns>
		public async Task<deleted_response> DeleteFineTuningJobAsync(string id, CancellationToken cancellationToken = default)
			=> await DeleteAsync<deleted_response>(fineTuningJobsPath, id, cancellationToken);

		#endregion

		#region Usage Operations

		/// <summary>
		/// Gets usage information from the OpenAI API.
		/// </summary>
		/// <returns>Usage response.</returns>
		public async Task<List<usage_response>> GetUsageAsync()
			=> await GetAsyncWithTask<usage_response>(usagePath);

		#endregion
	}
}
