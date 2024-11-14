#if NETFRAMEWORK
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
#else
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
#endif

using System;
using Embeddings;
using Embeddings.Embedding;
using System.IO;
using System.Linq;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.DataClient;
using System.Data;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingSynchronizer
	{

		public async Task ExportDatabaseToFolder(EmbeddingsItem item, string exportFolderPath, CancellationToken cancellationToken = default)
		{
			var target = AssemblyInfo.ExpandPath(item.Target);
			var connectionString = SqlInitHelper.IsPortable(target)
				? SqlInitHelper.PathToConnectionString(target)
				: target;
			var db = SqlInitHelper.NewEmbeddingsContext(connectionString);
			await ExportDatabaseToFolder(db, exportFolderPath);
		}

		public async Task ImportFolderToDatabase(EmbeddingsItem item, string exportFolderPath, CancellationToken cancellationToken = default)
		{
			var target = AssemblyInfo.ExpandPath(item.Target);
			var connectionString = SqlInitHelper.IsPortable(target)
				? SqlInitHelper.PathToConnectionString(target)
				: target;
			var db = SqlInitHelper.NewEmbeddingsContext(connectionString);
			await ImportFolderToDatabase(db, exportFolderPath);
		}

		/// <summary>
		/// Exports data from the database to the specified folder, grouping by GroupName.
		/// Excludes the 'Text' property from FilePart records to reduce size.
		/// </summary>
		/// <param name="exportFolderPath">The folder path to export data to.</param>
		public async Task ExportDatabaseToFolder(EmbeddingsContext db, string exportFolderPath, CancellationToken cancellationToken = default)
		{
			// Ensure export folder exists
			if (!Directory.Exists(exportFolderPath))
			{
				Directory.CreateDirectory(exportFolderPath);
			}
			await Task.Delay(0);
			// Get distinct GroupNames to process each group separately
			var groupNames = db.FileParts
				.Select(fp => fp.GroupName)
				.Distinct()
				.ToList();

			// Process each group individually to handle large datasets efficiently
			foreach (var groupName in groupNames)
			{
				if (cancellationToken.IsCancellationRequested)
					return;
				// Fetch FileParts for the current group, excluding the 'Text' and 'Embedding' properties
				var groupFileParts = db.FileParts
					.Where(fp => fp.GroupName == groupName)
					.Select(fp => new FilePart
					{
						Id = fp.Id,
						GroupName = fp.GroupName,
						GroupFlag = fp.GroupFlag,
						FileId = fp.FileId,
						Index = fp.Index,
						Count = fp.Count,
						HashType = fp.HashType,
						Hash = fp.Hash,
						State = fp.State,
						// Exclude 'Text'
						TextTokens = fp.TextTokens,
						EmbeddingModel = fp.EmbeddingModel,
						EmbeddingSize = fp.EmbeddingSize,
						// Exclude 'Embedding'
						IsEnabled = fp.IsEnabled,
						Created = fp.Created,
						Modified = fp.Modified,
						Timestamp = fp.Timestamp
					})
					.ToList();

				// Write each FilePart as a JSON line
				var sb = new StringBuilder();
				foreach (var fp in groupFileParts)
				{
					var json = Client.Serialize(fp);
					sb.AppendLine(json);
				}
				var outputFilePath = Path.Combine(exportFolderPath, $"{groupName}.jsonl");
				var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
				SettingsHelper.WriteIfDifferent(outputFilePath, bytes);
			}
		}

		/// <summary>
		/// Imports data from the specified folder into the database, synchronizing records.
		/// Utilizes the 'State' property to mark records for processing and deletion.
		/// </summary>
		/// <param name="importFolderPath">The folder path to import data from.</param>
		public async Task ImportFolderToDatabase(EmbeddingsContext db, string importFolderPath, CancellationToken cancellationToken = default)
		{
			if (!Directory.Exists(importFolderPath))
			{
				throw new DirectoryNotFoundException($"Import folder not found: {importFolderPath}");
			}

#if NETFRAMEWORK
			var connection = db.Database.Connection;
#else
			var connection = db.Database.GetDbConnection();
#endif

			var isPortable = SqlInitHelper.IsPortable(connection.ConnectionString);
			var schema = isPortable ? "" : "[Embedding].";
			var filePartTable = $"{schema}[{nameof(FilePart)}]";
			var fileTable = $"{schema}[{nameof(Embeddings.Embedding.File)}]";
			var groupTable = $"{schema}[{nameof(Group)}]";


			// Mark all existing FileParts as needing processing/deletion
			await Execute(db, $"UPDATE {filePartTable} SET [State] = @p0", (int)ProgressStatus.Started);

			// Read all .jsonl files in the folder
			var jsonlFiles = Directory.GetFiles(importFolderPath, "*.jsonl");

			// Begin a transaction to ensure data integrity
			using (var transaction = db.Database.BeginTransaction())
			{
				try
				{
					// Enable Identity Insert to allow setting the 'Id' property
					await Execute(db, $"SET IDENTITY_INSERT {filePartTable} ON");

					foreach (var jsonlFile in jsonlFiles)
					{
						if (cancellationToken.IsCancellationRequested)
							return;
						using (var reader = new StreamReader(jsonlFile))
						{
							string line;
							while ((line = reader.ReadLine()) != null)
							{
								var item = Client.Deserialize<FilePart>(line);

								// Check if FilePart exists in the database based on 'Id'
								var existingFp = db.FileParts.SingleOrDefault(fp => fp.Id == item.Id);

								if (existingFp != null)
								{
									// Update existing FilePart
									item.State = (int)ProgressStatus.Updated;
									JocysCom.ClassLibrary.Runtime.RuntimeHelper.CopyProperties(item, existingFp, true);
								}
								else
								{
									item.State = (int)ProgressStatus.Created;
									db.FileParts.Add(item);
								}
							}
						}

						// Save changes periodically to handle large datasets
						db.SaveChanges();
					}

					// Disable Identity Insert after importing data
					await Execute(db, $"SET IDENTITY_INSERT {filePartTable} OFF");

					// Commit the transaction
					transaction.Commit();
				}
				catch (Exception ex)
				{
					// Rollback the transaction on error
					transaction.Rollback();
					throw new Exception("An error occurred during import. Transaction has been rolled back.", ex);
				}
			}
			// Remove any FileParts that are still marked as 'Started' (i.e., were not updated or added)
			await Execute(db, $"DELETE FROM {filePartTable} WHERE [State] = @p0", (int)ProgressStatus.Started);

			// Reset 'State' to 'None' for all records
			await Execute(db, $"UPDATE {filePartTable} SET [State] = @p0", (int)ProgressStatus.None);
		}

		private async Task<int> Execute(EmbeddingsContext db, string commandText, params object[] args)
		{
#if NETFRAMEWORK
			var connection = db.Database.Connection;
#else
			var connection = db.Database.GetDbConnection();
#endif
			var command = connection.CreateCommand();
			if (connection.State != ConnectionState.Open)
				connection.Open();
			command.CommandText = commandText;
			foreach (var arg in args)
				command.Parameters.Add(arg);
			var rowsAffected = await command.ExecuteNonQueryAsync();
			return rowsAffected;
		}




	}
}
