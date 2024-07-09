using Embeddings;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.DataFunctions;
using Embeddings.Embedding;
using Microsoft.ML;
using JocysCom.ClassLibrary.Security;
using System.Threading;
using System.Text;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.DataClient.Common;
using JocysCom.ClassLibrary.Runtime;
using System.Collections.ObjectModel;
using JocysCom.ClassLibrary.Collections;
using JocysCom.VS.AiCompanion.Engine.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions;
using System.Windows.Threading;


#if NETFRAMEWORK
using System.Data.SQLite;
#else
using Microsoft.EntityFrameworkCore;
#endif

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingHelper
	{

		public TemplateItem Item { get; set; }

		public static async Task<ProgressStatus> UpdateEmbedding(
			EmbeddingsContext db,
			string fileName,
			System.Security.Cryptography.SHA256 algorithm,
			AiService service, string modelName,
			string embeddingGroupName, EmbeddingGroupFlag embeddingGroupFlag,
			CancellationToken cancellationToken = default
		)
		{
			var fi = new FileInfo(fileName);
			var file = db.Files.FirstOrDefault(x =>
				// Select item by index.
				x.GroupName == embeddingGroupName &&
				x.GroupFlag == (int)embeddingGroupFlag &&
				x.Url == fi.FullName);
			var fileHash = HashHelper.GetHashFromFile(algorithm, fileName);
			var fileHashDb = EmbeddingBase.GetHashByName(file?.Hash, file?.HashType);
			// If file found but different.
			if (fileHashDb != null && !fileHashDb.SequenceEqual(fileHash))
			{
				// Remove parts and file.
				var filePartsToDelete = db.FileParts.Where(x => x.FileId == file.Id).ToList();
				if (filePartsToDelete.Any())
				{
					foreach (var filePartToDelete in filePartsToDelete)
						db.FileParts.Remove(filePartToDelete);
					await db.SaveChangesAsync();
				}
				db.Files.Remove(file);
				file = null;
			}
			// If there is no such file in database then...
			if (file == null)
			{
				// Add new file.
				file = new Embeddings.Embedding.File();
				db.Files.Add(file);
				file.Timestamp = DateTime.UtcNow.Ticks;
				file.Name = fi.Name;
				file.Url = fi.FullName;
				file.GroupName = embeddingGroupName;
				file.GroupFlag = (long)embeddingGroupFlag;
				file.HashType = EmbeddingBase.SHA2_256;
				file.Hash = fileHash;
				file.Size = fi.Length;
				file.IsEnabled = true;
				file.Created = fi.CreationTime.ToUniversalTime();
				file.Modified = fi.LastWriteTime.ToUniversalTime();
			}
			file.State = (int)ProgressStatus.Completed;
			await db.SaveChangesAsync();
			// Process parts.
			var aiModel = Global.AppSettings.AiModels.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			var parts = GetParts(fi.FullName, aiModel.MaxInputTokens == 0 ? 2048 : aiModel.MaxInputTokens);
			var input = parts.Select(x => x.Text);
			// GetPart hashed from the database.
			var targetFileParts = db.FileParts
				.Where(x => x.FileId == file.Id)
				.OrderBy(x => x.Index)
				.ToList();
			var targetFilePartHashes = targetFileParts
				.Select(x => EmbeddingBase.GetHashByName(x.Hash, x.HashType))
				.ToList();
			var sourceFilePartHashes = input
				.Select(x => algorithm.ComputeHash(System.Text.Encoding.Unicode.GetBytes(x)))
				.ToList();
			// If all hashes match then...
			if (targetFilePartHashes.Count == sourceFilePartHashes.Count &&
				!targetFilePartHashes.Where((x, i) => !x.SequenceEqual(sourceFilePartHashes[i])).Any())
			{
				foreach (var targetFilePart in targetFileParts)
					targetFilePart.State = (int)ProgressStatus.Completed;
				await db.SaveChangesAsync();
				return ProgressStatus.Skipped;
			}
			// Remove parts.
			foreach (var targetFilePart in targetFileParts)
				db.FileParts.Remove(targetFilePart);
			await db.SaveChangesAsync();
			// Fill with updated records.
			var client = new Client(service);
			var results = await client.GetEmbedding(modelName, input, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
				return ProgressStatus.Canceled;
			var now = DateTime.Now;
			foreach (var key in results.Keys)
			{
				var ipart = parts[key];
				var vectors = results[key];
				var part = new Embeddings.Embedding.FilePart();
				part.Timestamp = DateTime.UtcNow.Ticks;
				part.Embedding = SqlInitHelper.VectorToBinary(vectors);
				part.GroupName = embeddingGroupName;
				part.GroupFlag = (long)embeddingGroupFlag;
				part.FileId = file.Id;
				part.EmbeddingModel = modelName;
				part.EmbeddingSize = vectors.Length;
				part.GroupFlag = (int)embeddingGroupFlag;
				part.Index = key;
				part.Count = parts.Length;
				part.HashType = EmbeddingBase.SHA2_256;
				part.Hash = sourceFilePartHashes[key];
				part.IsEnabled = true;
				file.State = (int)ProgressStatus.Completed;
				part.Created = now.ToUniversalTime();
				part.Modified = now.ToUniversalTime();
				part.Text = ipart.Text;
				part.TextTokens = ipart.TextTokens;
				db.FileParts.Add(part);
			}
			await db.SaveChangesAsync();
			return ProgressStatus.Updated;
		}

		public string Log { get; set; } = "";
		public List<Embeddings.Embedding.File> Files { get; set; }
		public List<Embeddings.Embedding.FilePart> FileParts { get; set; }

		public static FilePart[] GetParts(string path, int maxTokensPerChunk)
		{
			var mlContext = new MLContext();
			var fh = new FileHelper();
			var result = fh.ReadFileAsPlainText(path);
			var content = result?.Data;
			if (string.IsNullOrEmpty(content))
				return Array.Empty<FilePart>();
			//var data = new List<TextData> { new TextData { Text = content } };
			//var dataView = mlContext.Data.LoadFromEnumerable(data);
			// Tokenize the text
			// Define your custom list of separators
			//char[] separators = new char[] {
			//	' ', ',', '.', ';', ':', '!', '?', '-', '(', ')', '[', ']', '{', '}', '\"', '\'', '\n', '\t',
			//	'“', '”', '‘', '’', '\\', '/', '@', '#' };
			// Generate dynamic separators based on the content
			//var separators = GetDynamicSeparators(content);
			//var textPipeline = mlContext.Transforms.Text.TokenizeIntoWords("Tokens", nameof(TextData.Text), separators);
			//var textTransformer = textPipeline.Fit(dataView);
			//var transformedData = textTransformer.Transform(dataView);
			//var tokens = mlContext.Data.CreateEnumerable<TokenizedTextData>(transformedData, reuseRowObject: false).First().Tokens;
			int tokensCount;
			var tokens = new List<string>();
			ClientHelper.GetTokens(content, out tokensCount, ref tokens);
			// Chunk the tokens
			var chunks = ChunkTokens(tokens.ToArray(), (int)(maxTokensPerChunk * 0.80));
			return chunks.Select(x => new FilePart()
			{
				Text = string.Join("", x),
				TextTokens = x.Length

			}).ToArray();
		}

		private static char[] GetDynamicSeparators(string content)
		{
			var separatorSet = new HashSet<char>();
			foreach (var c in content)
				if (!char.IsLetterOrDigit(c))
					separatorSet.Add(c);
			return separatorSet.ToArray();
		}

		static IEnumerable<string[]> ChunkTokens(string[] tokens, int maxTokensPerChunk)
		{
			for (int i = 0; i < tokens.Length; i += maxTokensPerChunk)
			{
				yield return tokens.Skip(i).Take(maxTokensPerChunk).ToArray();
			}
		}

		class TextData
		{
			public string Text { get; set; }
		}

		class TokenizedTextData
		{
			public string[] Tokens { get; set; }
		}

		public async Task<OperationResult<string>> SearchEmbeddingsToSystemMessage(string message, int skip, int take)
		{
			try
			{
				var embeddingItem = Global.Embeddings.Items.FirstOrDefault(x => x.Name == Item.EmbeddingName);
				if (embeddingItem == null)
					return new OperationResult<string>($"Embedding '{Item.EmbeddingName}' settings not found!");
				var result = await SearchEmbeddingsToSystemMessage(embeddingItem,
					Item.EmbeddingGroupName, Item.EmbeddingGroupFlag,
					message, skip, take);
				return new OperationResult<string>(result);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(new Exception(ex.Message));
			}
		}

		public async Task<string> SearchEmbeddingsToSystemMessage(EmbeddingsItem item,
			string groupName,
			EmbeddingGroupFlag groupFlag, string message, int skip, int take)
		{
			await SearchEmbeddings(item,
				groupName, groupFlag,
				message, skip, take);
			var systemMessage = "";
			if (FileParts == null || FileParts.Count == 0)
				return systemMessage;
			systemMessage += item.Instructions;
			systemMessage += "\r\n\r\n";
			foreach (var filePart in FileParts)
			{
				var file = Files.Where(x => x.Id == filePart.FileId).FirstOrDefault();
				systemMessage += "\r\n";
				systemMessage += ConvertToChunkString(file?.Url, filePart);
				systemMessage += "\r\n\r\n";
			}
			return systemMessage;
		}


		/// <summary>
		/// Convert to flat string representation.
		/// </summary>
		public static string ConvertToChunkString(string path, FilePart filePart)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"-----BEGIN FILE CHUNK {filePart.Index + 1} OF {filePart.Count}: {path}-----");
			sb.Append(filePart.Text);
			if (!filePart.Text.EndsWith(Environment.NewLine))
				sb.AppendLine();
			sb.AppendLine($"-----END FILE CHUNK {filePart.Index + 1} OF {filePart.Count}: {path}-----");
			return sb.ToString();
		}

		public async Task SearchEmbeddings(
			EmbeddingsItem item,
			string groupName,
			EmbeddingGroupFlag groupFlag,
			string message,
			int skip, int take,
			CancellationToken cancellationToken = default)
		{
			try
			{
				Log = "Converting message to embedding vectors...";
				if (string.IsNullOrWhiteSpace(message))
				{
					Log += " Message is empty.\r\n";
					return;
				}
				var input = new List<string> { message };
				var client = new Client(item.AiService);
				var results = await client.GetEmbedding(item.AiModel, input);
				Log += " Done.\r\n";
				var target = AssemblyInfo.ExpandPath(item.Target);
				var connectionString = SqlInitHelper.IsPortable(target)
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				var db = SqlInitHelper.NewEmbeddingsContext(connectionString);
				var vectors = results[0];
				Log += "Searching on database...";
				var useClr = false;
				var isPortable = SqlInitHelper.IsPortable(item.Target);
				if (!isPortable)
				{
					var connection = SqlInitHelper.NewConnection(connectionString);
					await connection.OpenAsync();
					useClr = !SqlInitHelper.IsAzureSQL(connection);
					connection.Close();
				}
				if (useClr)
				{
					FileParts = await db.sp_getSimilarFileParts(
						groupName, (long)groupFlag,
						vectors, skip, take, cancellationToken
					);
				}
				else
				{
					var ids = await SqlInitHelper.GetSimilarFileEmbeddings(isPortable, connectionString, groupName, groupFlag, vectors, item.Take);
					FileParts = db.FileParts
						.Where(x => ids.Contains(x.Id))
						.ToList()
						.OrderBy(x => ids.IndexOf(x.Id))
						.ToList();
				}
				var fileIds = FileParts.Select(x => x.FileId).Distinct().ToArray();
				Files = db.Files.Where(x => fileIds.Contains(x.Id)).ToList();
				Log += " Done...";
			}
			catch (System.Exception ex)
			{
				Log = ex.ToString();
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


		public static void ApplyDatabase(string groupName, ObservableCollection<KeyValue<EmbeddingGroupFlag, string>> property)
		{
			// Run the time-consuming operations asynchronously
			Task.Run(() =>
			{
				var ei = Global.Embeddings.Items.FirstOrDefault(x => x.EmbeddingGroupName == groupName);
				if (ei == null)
					return;
				var flags = GetFlags(ei);
				// Update the UI thread
				Dispatcher.CurrentDispatcher.Invoke(() =>
				{
					var items = property.ToArray();
					foreach (var item in items)
					{
						var flagName = flags.FirstOrDefault(x => x.Flag == (long)item.Key)?.FlagName ?? string.Empty;
						var description = Attributes.GetDescription(item.Key);
						if (!string.IsNullOrEmpty(flagName))
						{
							description += ": " + flagName;
						}

						item.Value = description;
					}
				});
			});
		}

		public static void ApplyDatabase(string groupName, ObservableCollection<EnumComboBox.CheckBoxViewModel> property)
		{
			var ei = Global.Embeddings.Items.FirstOrDefault(x => x.EmbeddingGroupName == groupName);
			var flags = GetFlags(ei);
			var items = property.ToArray();
			foreach (var item in items)
			{
				var flagName = flags.FirstOrDefault(x => x.Flag == Convert.ToInt64(item.Value))?.FlagName ?? "";
				var description = Attributes.GetDescription(item.Value);
				if (!string.IsNullOrEmpty(flagName))
					description += ": " + flagName;
				item.Description = description;
			}
		}

		private static Embeddings.Embedding.Group[] GetFlags(EmbeddingsItem ei)
		{
			if (ei?.IsEnabled != true || string.IsNullOrWhiteSpace(ei?.Target))
				return Array.Empty<Embeddings.Embedding.Group>();
			try
			{
				var target = AssemblyInfo.ExpandPath(ei.Target);
				var connectionString = SqlInitHelper.IsPortable(target)
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				var db = SqlInitHelper.NewEmbeddingsContext(connectionString);
				var items = db.Groups.Where(x => x.Name == ei.EmbeddingGroupName).ToArray();
				return items;
			}
			catch (Exception)
			{
				return Array.Empty<Embeddings.Embedding.Group>();
			}
		}


	}
}
