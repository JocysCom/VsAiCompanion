using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Files;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Functions that enable AI to perform indexed searches. Use "Windows Search Settings" to add more folders to your search.
	/// </summary>
	public partial class Search
	{

		#region Windows Index

		/// <summary>
		/// Database path. Set by external program.
		/// </summary>
		public static string _databasePath;

		/// <summary>
		/// LiteDB file extension.
		/// </summary>
		public const string LitedbExt = ".litedb";

		//public static Dictionary<string, string> GetIndexList() => new Dictionary<string, string>();

		/// <summary>
		/// Index a specific folder. Returns true if successful.
		/// </summary>
		/// <param name="indexName">Index name.</param>
		/// <param name="folderPath">Folder path.</param>
#if DEBUG
		[RiskLevel(RiskLevel.Low)]
#endif
		public static bool IndexFolder(string indexName, string folderPath)
		{
			var di = new DirectoryInfo(_databasePath);
			if (!di.Exists)
				di.Create();

			var connectionString = Path.Combine(_databasePath, indexName + LitedbExt);
			using (var db = new LiteDatabase(connectionString))
			{
				var filesCollection = db.GetCollection<DocItem>(indexName);

				// Ensure we have an index on the Path and Content fields
				filesCollection.EnsureIndex(x => x.FullName);
				filesCollection.EnsureIndex(x => x.ContentData);

				foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
				{
					var fileInfo = new FileInfo(filePath);
					// Insert or update the file document in the LiteDB collection
					var doc = new DocItem()
					{
						FullName = fileInfo.FullName,
					};
					doc.LoadFileInfo();
					doc.LoadData();
					filesCollection.Upsert(doc);
				}
			}
			return true;
		}

		/// <summary>
		/// Search an index and return search results.
		/// </summary>
		/// <param name="indexName">Index name.</param>
		/// <param name="searchString">Search string.</param>
#if DEBUG
		[RiskLevel(RiskLevel.Low)]
#endif
		public static List<string> SearchIndex(string indexName, string searchString)
		{
			var di = new DirectoryInfo(_databasePath);
			if (!di.Exists)
				di.Create();
			var connectionString = Path.Combine(_databasePath, indexName + LitedbExt);
			using (var db = new LiteDatabase(connectionString))
			{
				var filesCollection = db.GetCollection<DocItem>(indexName);
				// Perform the search within the file contents, as well as the file name
				var results = filesCollection.Find(x =>
					x.FullName.Contains(searchString) ||
					x.ContentData.Contains(searchString));
				// Print out the results
				foreach (var file in results)
				{
					Console.WriteLine($"Found: {file.FullName}");
				}
				return new List<string>(results.Select(x => x.FullName));
			}
		}

		/// <summary>
		/// Searches the Windows Index for files matching the specified criteria. This method allows for extensive search capabilities, including text content, file metadata, and more.
		/// </summary>
		/// <param name="contents">Contents of the file. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="itemName">The name of the item, typically the file name including the extension. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="itemPath">The full path of the item, suitable for display to the user. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="itemTypeText">A text description of the item type, e.g., "JPEG image". Uses CONTAINS in the query for partial matching.</param>
		/// <param name="fileExtension">The file extension of the item. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="sizeMin">The minimum size of the item, in bytes.</param>
		/// <param name="sizeMax">The maximum size of the item, in bytes.</param>
		/// <param name="author">The author of the document. Applicable to documents that store this metadata. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="title">The title of the document. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="comment">Any comment associated with the file. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="dateCreatedStart">The start date for when the item was created.</param>
		/// <param name="dateCreatedEnd">The end date for when the item was created.</param>
		/// <param name="dateModifiedStart">The start date for when the item was last modified.</param>
		/// <param name="dateModifiedEnd">The end date for when the item was last modified.</param>
		/// <param name="dateAccessedStart">The start date for when the item was last accessed.</param>
		/// <param name="dateAccessedEnd">The end date for when the item was last accessed.</param>
		/// <returns>A list of <see cref="IndexedFileInfo"/> objects that match the specified search criteria.</returns>
		/// <remarks>
		/// This method leverages the Windows Search Index, offering a powerful search functionality across various file properties.
		/// For properties like dates and size, where partial matching isn't applicable, the method uses SQL expressions to define ranges.
		/// </remarks>
		// <param name="keywords">The keywords associated with the file. This can be a collection of keywords. Each keyword uses CONTAINS in the query for partial matching.</param>
		[RiskLevel(RiskLevel.Low)]
		public static List<IndexedFileInfo> SearchWindowsIndex(
			string contents = null,
			string itemName = null, string itemPath = null, string itemTypeText = null,
			string fileExtension = null, long? sizeMin = null, long? sizeMax = null,
			string author = null, string title = null, string comment = null,
			DateTime? dateCreatedStart = null, DateTime? dateCreatedEnd = null,
			DateTime? dateModifiedStart = null, DateTime? dateModifiedEnd = null,
			DateTime? dateAccessedStart = null, DateTime? dateAccessedEnd = null
		//string[] keywords = null,
		)
		{
			var whereClauses = new List<string>();
			var parameters = new List<OleDbParameter>();

			if (!string.IsNullOrEmpty(contents))
			{
				whereClauses.Add("CONTAINS(System.Search.Contents, @Contents)");
				parameters.Add(new OleDbParameter("@Contents", $"\"*{contents}*\""));
			}

			if (!string.IsNullOrEmpty(itemName))
			{
				whereClauses.Add("CONTAINS(System.ItemNameDisplay, @ItemNameDisplay)");
				parameters.Add(new OleDbParameter("@ItemNameDisplay", $"\"*{itemName}*\""));
			}

			if (!string.IsNullOrEmpty(itemPath))
			{
				whereClauses.Add("CONTAINS(System.ItemPathDisplay, @ItemPathDisplay)");
				parameters.Add(new OleDbParameter("@ItemPathDisplay", $"\"*{itemPath}*\""));
			}

			if (!string.IsNullOrEmpty(itemTypeText))
			{
				whereClauses.Add("CONTAINS(System.ItemTypeText, @ItemTypeText)");
				parameters.Add(new OleDbParameter("@ItemTypeText", $"\"*{itemTypeText}*\""));
			}

			if (!string.IsNullOrEmpty(fileExtension))
			{
				whereClauses.Add("CONTAINS(System.FileExtension, @FileExtension)");
				parameters.Add(new OleDbParameter("@FileExtension", $"\"*{fileExtension}*\""));
			}

			if (!string.IsNullOrEmpty(title))
			{
				whereClauses.Add("CONTAINS(System.Title, @Title)");
				parameters.Add(new OleDbParameter("@Title", $"\"*{title}*\""));
			}

			/*
			foreach (var keyword in keywords)
			{
				if (!string.IsNullOrEmpty(keyword))
				{
					whereClauses.Add("CONTAINS(System.Search.Keywords, @Keyword)");
					parameters.Add(new OleDbParameter("@Keyword", $"\"*{keyword}*\""));
				}
			}
			*/

			if (!string.IsNullOrEmpty(comment))
			{
				whereClauses.Add("CONTAINS(System.Comment, @Comment)");
				parameters.Add(new OleDbParameter("@Comment", $"\"*{comment}*\""));
			}


			if (!string.IsNullOrEmpty(author))
			{
				whereClauses.Add("CONTAINS(System.Author, @Author)");
				parameters.Add(new OleDbParameter("@Author", $"\"*{author}*\""));
			}

			if (sizeMin.HasValue)
			{
				whereClauses.Add("System.Size >= @SizeMin");
				parameters.Add(new OleDbParameter("@SizeMin", sizeMin.Value));
			}

			if (sizeMax.HasValue)
			{
				whereClauses.Add("System.Size <= @SizeMax");
				parameters.Add(new OleDbParameter("@SizeMax", sizeMax.Value));
			}

			if (dateCreatedStart.HasValue)
			{
				whereClauses.Add("System.DateCreated >= @DateCreatedStart");
				parameters.Add(new OleDbParameter("@DateCreatedStart", dateCreatedStart.Value));
			}

			if (dateCreatedEnd.HasValue)
			{
				whereClauses.Add("System.DateCreated <= @DateCreatedEnd)");
				parameters.Add(new OleDbParameter("@DateCreatedEnd", dateCreatedEnd.Value));
			}

			if (dateAccessedStart.HasValue)
			{
				whereClauses.Add("System.DateAccessed >= @DateAccessedStart");
				parameters.Add(new OleDbParameter("@DateAccessedStart", dateAccessedStart.Value));
			}

			if (dateAccessedEnd.HasValue)
			{
				whereClauses.Add("System.DateAccessed <= @DateAccessedEnd");
				parameters.Add(new OleDbParameter("@DateAccessedEnd", dateAccessedEnd.Value));
			}

			if (dateModifiedStart.HasValue)
			{
				whereClauses.Add("System.DateModified >= @DateModStart");
				parameters.Add(new OleDbParameter("@DateModStart", dateModifiedStart.Value));
			}

			if (dateModifiedEnd.HasValue)
			{
				whereClauses.Add("System.DateModified <= @DateModEnd");
				parameters.Add(new OleDbParameter("@DateModEnd", dateModifiedEnd.Value));
			}

			var query = $@"
            SELECT TOP 100
				System.ItemNameDisplay, System.ItemPathDisplay,
				System.FileExtension, System.ItemTypeText,
				System.Size, System.Author, System.Title,
				System.DateCreated, System.DateModified, System.DateAccessed
            FROM SystemIndex 
            WHERE {string.Join(" AND ", whereClauses)}";


			var results = ExecuteQuery(query, parameters);
			return results;
		}

		private static List<IndexedFileInfo> ExecuteQuery(string query, List<OleDbParameter> parameters)
		{
			var results = new List<IndexedFileInfo>();
			string connectionString = @"Provider=Search.CollatorDSO;Extended Properties='Application=Windows';";

			using (var connection = new OleDbConnection(connectionString))
			{
				var queryString = GetSqlCommand(query, parameters);
				var command = new OleDbCommand(queryString, connection);
				//foreach (var parameter in parameters)
				//{
				//	command.Parameters.Add(parameter);
				//}

				try
				{
					connection.Open();
					var reader = command.ExecuteReader();

					while (reader.Read())
					{
						var fi = new IndexedFileInfo();
						fi.ItemName = reader["System.ItemNameDisplay"] as string;
						fi.ItemPathDisplay = reader["System.ItemPathDisplay"] as string;
						fi.ItemTypeText = reader["System.ItemTypeText"] as string;
						fi.FileExtension = reader["System.FileExtension"] as string;
						var size = reader.IsDBNull(reader.GetOrdinal("System.Size"))
							? null
							: reader["System.Size"].ToString();
						decimal decimalSize;
						fi.Size = !decimal.TryParse(size, out decimalSize)
							? 0
							: (long)decimalSize;
						fi.Author = reader["System.Author"] as string;
						fi.Title = reader["System.Title"] as string;
						fi.DateCreated = reader["System.DateCreated"] as DateTime?;
						fi.DateModified = reader["System.DateModified"] as DateTime?;
						fi.DateAccessed = reader["System.DateAccessed"] as DateTime?;
						results.Add(fi);
					}
					reader.Close();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"An error occurred: {ex.Message}");
				}
			}

			return results;
		}

		private static string GetSqlCommand(string query, List<OleDbParameter> parameters)
		{
			foreach (OleDbParameter parameter in parameters)
			{
				string parameterName = parameter.ParameterName;
				string parameterValue = parameter.Value.ToString();
				// If the parameter.Value is a string, you should escape any single quotes and wrap it in single quotes
				if (parameter.Value is string)
				{
					parameterValue = parameterValue.Replace("'", "''"); // Sanitize single quotes
					parameterValue = $"'{parameterValue}'"; // Wrap in single quotes for SQL string
				}
				// For datetime parameters, ensure to format the date correctly for SQL.
				else if (parameter.Value is DateTime)
				{
					parameterValue = ((DateTime)parameter.Value).ToString("yyyy-MM-dd HH:mm:ss");
					parameterValue = $"'{parameterValue}'";
				}
				// Add other types and their formatting as needed (e.g., decimals might need formatting)
				// Replace the parameter in the query
				query = query.Replace(parameter.ParameterName, parameterValue);
			}
			return query;
		}

		#endregion

		#region Search Files

		/// <summary>
		/// Searches for files  and write search results as csv file.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="searchPath">Directory path to search for files.</param>
		/// <param name="searchPattern">Search pattern for files (e.g., "*.txt").</param>
		/// <param name="allDirectories">Whether to search all subdirectories.</param>
		/// <param name="includePatterns">Patterns to include.</param>
		/// <param name="excludePatterns">Patterns to exclude.</param>
		/// <param name="useGitIgnore">Whether to respect .gitignore files.</param>
		/// <returns>Number of files inserted.</returns>
		[RiskLevel(RiskLevel.High)]
		public async Task<OperationResult<int>> SearchAndSaveFilesAsCsv(
			string path,
			string searchPath,
			string searchPattern,
			bool allDirectories = false,
			string includePatterns = null,
			string excludePatterns = null,
			bool useGitIgnore = false
		)
		{
			var fullName = nameof(FileInfo.FullName);
			var name = nameof(FileInfo.Name);
			var length = nameof(FileInfo.Length);
			var creationTime = nameof(FileInfo.CreationTime);
			var lastWriteTime = nameof(FileInfo.LastWriteTime);
			var fileHelper = new FileHelper();
			// Find the files.
			List<FileInfo> files;
			try
			{
				await Task.Delay(0);
				files = fileHelper.FindFiles(
					searchPath,
					searchPattern,
					allDirectories,
					includePatterns,
					excludePatterns,
					useGitIgnore
				).OrderBy(x => x.FullName).ToList();
				var table = new DataTable();
				table.Columns.Add(fullName, typeof(string));
				table.Columns.Add(name, typeof(string));
				table.Columns.Add(length, typeof(long));
				table.Columns.Add(creationTime, typeof(DateTime));
				table.Columns.Add(lastWriteTime, typeof(DateTime));
				foreach (var file in files)
					table.Rows.Add(file.FullName, file.Name, file.Length, file.CreationTimeUtc, file.LastWriteTime);
				var csvContents = CsvHelper.Write(table);
				System.IO.File.WriteAllText(path, csvContents);
				return new OperationResult<int>(files.Count());
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}
		}

		#endregion

		#region Embeddings


		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<string, int, int, Task<OperationResult<string>>> SearchEmbeddingsCallback { get; set; }

		/// <summary>
		/// Search the embedding database for relevant content in files and documents.
		/// </summary>
		/// <param name="message">Search query.</param>
		/// <param name="skip">Number of records to skip. Recommended: 0</param>
		/// <param name="take">Number of records to take. Recommended: 4</param>
		[RiskLevel(RiskLevel.Low)]
		public async Task<OperationResult<string>> SearchEmbeddings(
			string message,
			int skip,
			int take
			)
		{
			return await SearchEmbeddingsCallback(message, skip, take);
		}

		#endregion

	}

}
