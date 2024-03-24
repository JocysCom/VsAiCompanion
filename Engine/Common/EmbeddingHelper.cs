using Embeddings;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingHelper
	{

		public static async Task ConvertToEmbeddingsCSV(string path, string connectionString, AiService service, string modelName)
		{
			//var service = Global.AppSettings.AiServices.FirstOrDefault(x => x.BaseUrl.Contains("azure"));
			//var url = service.BaseUrl + $"openai/deployments/{model}/embeddings?api-version=2023-05-15";
			var files = Directory.GetFiles(path, "*.txt");
#if NETFRAMEWORK
			var db = new EmbeddingsContext();
			db.Database.Connection.ConnectionString = connectionString;
#else
			var db = EmbeddingsContext.Create(connectionString);
#endif
			var algorithm = System.Security.Cryptography.SHA256.Create();

			for (int i = 0; i < files.Count(); i++)
			{
				var fi = new FileInfo(files[i]);
				var file = db.Files.FirstOrDefault(x => x.Url == fi.FullName);
				var fileHash = JocysCom.ClassLibrary.Security.SHA256Helper.GetHashFromFile(fi.FullName);
				if (file == null)
				{
					file = new Embeddings.Embedding.File();
					db.Files.Add(file);
				}
				file.Name = fi.Name;
				file.Url = fi.FullName;
				file.GroupName = System.IO.Path.GetFileName(path);
				file.HashType = "SHA_256";
				file.Hash = fileHash;
				file.Size = fi.Length;
				file.State = (int)ProgressStatus.Completed;
				file.TextSize = fi.Length;
				file.IsEnabled = true;
				file.Created = fi.CreationTime.ToUniversalTime();
				file.Modified = fi.LastWriteTime.ToUniversalTime();
				file.TextSize = file.Size;
				db.SaveChanges();
				var text = File.ReadAllText(fi.FullName);
				var client = new Client(service);
				// Don't split file
				var input = new List<string> { text };
				var results = await client.GetEmbedding(modelName, input);
				var now = DateTime.Now;
				var partHash = algorithm.ComputeHash(System.Text.Encoding.Unicode.GetBytes(text));
				foreach (var result in results)
				{
					var part = new Embeddings.Embedding.FilePart();
					part.Embedding = VectorToBinary(result.Value);
					part.FileId = file.Id;
					part.EmbeddingModel = modelName;
					part.EmbeddingSize = result.Value.Length;
					part.Index = 0;
					part.Count = 1;
					part.HashType = "SHA_256";
					part.Hash = partHash;
					part.IsEnabled = true;
					part.Created = now.ToUniversalTime();
					part.Modified = now.ToUniversalTime();
					part.Text = input[0];
					db.FileParts.Add(part);
					db.SaveChanges();
				}
			}
		}

		#region Client

		//// Synchronous method to get embeddings
		//public static float[] GetEmbeddings(string url, string apiKey, string text, AiService service)
		//{
		//	if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(text))
		//		throw new ArgumentException("URL, API key, and text cannot be null or empty.");
		//	using (var _httpClient = new HttpClient())
		//	{
		//		// Add the necessary headers to the request
		//		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		//		// Create the JSON body
		//		var payload = new payload { text = text };
		//		var json = Client.Serialize(payload);
		//		var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
		//		// Make the POST request
		//		var response = _httpClient.PostAsync(url, httpContent).Result;
		//		response.EnsureSuccessStatusCode();
		//		// Read the response as a string
		//		string responseString = response.Content.ReadAsStringAsync().Result;
		//		// Deserialize the response into a list of embeddings
		//		var result = Client.Deserialize<embedding_response>(responseString);
		//		var embeddings = result.embeddings[0].embedding;
		//		return embeddings;
		//	}
		//}

		/// <summary>
		/// Convert embedding vectors to byte array.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <returns>Byte array.</returns>
		public static byte[] VectorToBinary(float[] vectors)
		{
			byte[] bytes = new byte[vectors.Length * sizeof(float)];
			Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
			return bytes;
		}

		private class @payload
		{
			public string text { get; set; }
		}

		// Helper class to deserialize the JSON response
		private class @embedding_response
		{
			public Embedding[] embeddings { get; set; }
		}

		private class Embedding
		{
			public float[] embedding { get; set; }
		}


		#endregion
	}
}
