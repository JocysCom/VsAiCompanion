using Embeddings;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.DataClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using System.Windows.Threading;
using System.Text.RegularExpressions;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions;


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
			EmbeddingsItem ei,
			EmbeddingsContext db,
			string fileName,
			CancellationToken cancellationToken = default
		)
		{
			var service = ei.AiService;
			var modelName = ei.AiModel;

			var fi = new FileInfo(fileName);

			var source = AssemblyInfo.ExpandPath(ei.Source);
			(var embeddingGroupName, var embeddingGroupFlagName) = GetGroupAndFlagNames(source, fi.FullName);
			if (ei.OverrideGroupName)
				embeddingGroupName = ei.EmbeddingGroupName;

			var embeddingGroupFlag = ei.EmbeddingGroupFlag;
			if (!ei.OverrideGroupFlag)
			{
				var groups = GetFlags(ei, embeddingGroupName);
				var group = groups.FirstOrDefault(x => x.Name == embeddingGroupName && x.FlagName == embeddingGroupFlagName);
				// Group flag not found.
				if (group == null)
				{
					var allFlags = (EmbeddingGroupFlag[])Enum.GetValues(typeof(EmbeddingGroupFlag));
					var freeFlag = allFlags
						.Except(groups.Select(x => (EmbeddingGroupFlag)x.Flag))
						.Except(new[] { EmbeddingGroupFlag.None })
						.First();
					var item = new Embeddings.Embedding.Group();
					item.Timestamp = DateTime.UtcNow.Ticks;
					item.Name = embeddingGroupName;
					item.Flag = (long)freeFlag;
					item.FlagName = embeddingGroupFlagName;
					db.Groups.Add(item);
					db.SaveChanges();
					embeddingGroupFlag = freeFlag;
				}
				else
				{
					embeddingGroupFlag = (EmbeddingGroupFlag)group.Flag;
				}
			}

			var file = db.Files.FirstOrDefault(x =>
				// Select item by index.
				x.GroupName == embeddingGroupName &&
				x.GroupFlag == (int)embeddingGroupFlag &&
				x.Url == fi.FullName);

			var algorithm = System.Security.Cryptography.SHA256.Create();
			var fileHash = HashHelper.GetHashFromFile(algorithm, fileName);
			algorithm.Dispose();
			//algorithm.Dispose();
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

			FilePart[] parts = null;
			var sourceFilePartHashes = new List<byte[]>();
			decimal tokenReduction = 0.80m;

			// Fill with updated records.
			var client = AiClientFactory.GetAiClient(service);
			Dictionary<int, float[]> results = null;
			int maxRetries = 3;

			// GetPart hashed from the database.
			var targetFileParts = db.FileParts
				.Where(x => x.FileId == file.Id)
				.OrderBy(x => x.Index)
				.ToList();
			var targetFilePartHashes = targetFileParts
				.Select(x => EmbeddingBase.GetHashByName(x.Hash, x.HashType))
				.ToList();
			var algorithm2 = System.Security.Cryptography.SHA256.Create();
			do
			{

				parts = GetParts(fi.FullName, aiModel.MaxInputTokens == 0 ? 2048 : aiModel.MaxInputTokens, tokenReduction);
				var inputs = parts.Select(x => x.Text);
				foreach (var input in inputs)
				{
					var hash = algorithm2.ComputeHash(System.Text.Encoding.Unicode.GetBytes(input));
					sourceFilePartHashes.Add(hash);
				}
				// If all hashes match then...
				if (targetFilePartHashes.Count == sourceFilePartHashes.Count &&
					!targetFilePartHashes.Where((x, i) => !x.SequenceEqual(sourceFilePartHashes[i])).Any())
				{
					foreach (var targetFilePart in targetFileParts)
						targetFilePart.State = (int)ProgressStatus.Completed;
					await db.SaveChangesAsync();
					return ProgressStatus.Skipped;
				}

				var opResults = await client.GetEmbedding(modelName, inputs, cancellationToken);
				results = opResults.Data;
				if (opResults?.Success == true || maxRetries-- <= 0 || cancellationToken.IsCancellationRequested)
					break;
				if (opResults.Errors.Any(x => (x ?? "").IndexOf("please reduce your prompt", StringComparison.OrdinalIgnoreCase) > -1))
				{
					tokenReduction -= 0.10m;
				}
				else if (opResults.Errors.Any(x => (x ?? "").IndexOf("exceeded call rate limit", StringComparison.OrdinalIgnoreCase) > -1))
				{
					// Wait 20 seconds.
					await Task.Delay(20000, cancellationToken);
				}
			}
			while (true);
			algorithm2.Dispose();

			if (cancellationToken.IsCancellationRequested)
				return ProgressStatus.Canceled;
			if (results == null)
				return ProgressStatus.Exception;

			// Remove old parts.
			foreach (var targetFilePart in targetFileParts)
				db.FileParts.Remove(targetFilePart);
			await db.SaveChangesAsync();


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

		public static FilePart[] GetParts(string path, int maxTokensPerChunk, decimal tokenReduction = 0.80m)
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
			Plugins.Core.Basic.GetTokens(content, out tokensCount, ref tokens);
			// Chunk the tokens
			var chunks = ChunkTokens(tokens.ToArray(), (int)(maxTokensPerChunk * tokenReduction));
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
				var client = AiClientFactory.GetAiClient(item.AiService);
				// TRy to get embeddings.
				Dictionary<int, float[]> results = null;
				int maxRetries = 3;
				do
				{
					var opResults = await client.GetEmbedding(item.AiModel, input);
					results = opResults.Data;
					if (opResults?.Success == true || maxRetries-- <= 0 || cancellationToken.IsCancellationRequested)
						break;
					Log += $"Errors: {string.Join("\r\n", opResults.Errors)}\r\n";
					// Wait 20 seconds.
					await Task.Delay(20000, cancellationToken);
				}
				while (true);
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
					using (var connection = SqlInitHelper.NewConnection(connectionString))
					{
						await connection.OpenAsync();
						useClr = !SqlInitHelper.IsAzureSQL(connection);
					}
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


		public static void UpdateGroupNamesFromDatabase(string embeddingName,
			ObservableCollection<string> property,
			params string[] groupsToAdd
			)
		{
			// Run the time-consuming operations asynchronously
			Task.Run(() =>
			{
				var ei = Global.Embeddings.Items.FirstOrDefault(x => x.Name == embeddingName);
				if (ei == null)
					return;
				var names = GetGroupNames(ei);
				// Find items in listB that are not in listA
				var itemsToAdd = groupsToAdd.Distinct().Except(names).ToArray();
				names.AddRange(itemsToAdd);
				names = names.OrderBy(x => x).ToList();
				// Update property on the UI thread.
				ControlsHelper.AppBeginInvoke(() =>
				{
					CollectionsHelper.Synchronize(names, property);
				});
			});
		}
		public static void UpdateGroupFlagsFromDatabase(string embeddingName, ObservableCollection<KeyValue<EmbeddingGroupFlag, string>> property)
		{
			// Run the time-consuming operations asynchronously
			Task.Run(() =>
			{
				var ei = Global.Embeddings.Items.FirstOrDefault(x => x.Name == embeddingName);
				if (ei == null)
					return;
				var flags = GetFlags(ei, ei?.EmbeddingGroupName);
				// Update the UI thread
				Dispatcher.CurrentDispatcher.Invoke(() =>
				{
					var items = property.ToArray();
					foreach (var item in items)
					{
						var description = Attributes.GetDescription(item.Key);
						var flagName = flags.FirstOrDefault(x => x.Flag == (long)item.Key)?.FlagName;
						if (flagName != null)
							description += ": " + flagName;
						item.Value = description;
					}
				});
			});
		}

		public static void UpdateGroupFlagsFromDatabase(string embeddingName, ObservableCollection<CheckBoxViewModel> property)
		{
			var ei = Global.Embeddings.Items.FirstOrDefault(x => x.Name == embeddingName);
			var flags = GetFlags(ei, ei?.EmbeddingGroupName);
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

		private static List<string> GetGroupNames(EmbeddingsItem ei)
		{
			var items = new List<string>();
			if (ei?.IsEnabled != true || string.IsNullOrWhiteSpace(ei?.Target))
				return items;
			try
			{
				var target = AssemblyInfo.ExpandPath(ei.Target);
				var connectionString = SqlInitHelper.IsPortable(target)
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				using (var db = SqlInitHelper.NewEmbeddingsContext(connectionString))
				{
					using (var connection = db.GetConnection())
					{
						connection.Open();
						var containsTable = SqlInitHelper.ContainsTable(nameof(Embeddings.Embedding.File), connection);
						if (containsTable)
							items = db.Files.Select(x => x.GroupName).Distinct().ToList();
					} // connection is disposed here
				} // db context is disposed here
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
			}
			return items;
		}

		private static Embeddings.Embedding.Group[] GetFlags(EmbeddingsItem ei, string groupName)
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
				var items = db.Groups.Where(x => x.Name == groupName).ToArray();
				return items;
			}
			catch (Exception)
			{
				return Array.Empty<Embeddings.Embedding.Group>();
			}
		}

		public static (string groupName, string flagName) GetGroupAndFlagNames(string rootPath, string filePath)
		{
			// Fix relative folder path.
			rootPath = rootPath.TrimEnd('\\') + "\\";
			// Get the directory name of the root path
			var rootDirectoryName = new DirectoryInfo(rootPath).Name;
			// Determine the group name based on the root path
			var groupName = string.IsNullOrEmpty(rootDirectoryName) || rootDirectoryName == rootPath.TrimEnd('\\')
				? "."
				: rootDirectoryName;
			// Get the relative path from the root path
			var relativePath = JocysCom.ClassLibrary.IO.PathHelper.GetRelativePath(rootPath, filePath);
			// Regular expression to match flag name in the relative path
			var regex = new Regex(@"^(?<flag>[^\\]+)?.*");
			var match = regex.Match(relativePath);
			if (match.Success)
			{
				// If the flag group is not available, return "."
				var flagName = match.Groups["flag"].Success ? match.Groups["flag"].Value : ".";
				return (groupName, flagName);
			}
			else
			{
				throw new InvalidOperationException("The provided file path does not match the expected format.");
			}
		}

	}
}
