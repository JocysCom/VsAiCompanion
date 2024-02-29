using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Index and search.
	/// </summary>
	public partial class Search
	{

		/// <summary>
		/// Database path. Set by external program.
		/// </summary>
		public static string _databasePath;

		//public static Dictionary<string, string> GetIndexList() => new Dictionary<string, string>();

		/// <summary>
		/// Index a specific folder. Returns true if successful.
		/// </summary>
		/// <param name="indexName">Index name.</param>
		/// <param name="folderPath">Folder path.</param>
		[RiskLevel(RiskLevel.Low)]
		public static bool IndexFolder(string indexName, string folderPath)
		{
			var di = new DirectoryInfo(_databasePath);
			if (!di.Exists)
				di.Create();

			var connectionString = Path.Combine(_databasePath, indexName + ".db");
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
						Name = fileInfo.Name,
						Size = fileInfo.Length,
						LastWrite = fileInfo.LastWriteTimeUtc,
					};
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
		[RiskLevel(RiskLevel.Low)]
		public static List<string> SearchIndex(string indexName, string searchString)
		{
			var di = new DirectoryInfo(_databasePath);
			if (!di.Exists)
				di.Create();
			var connectionString = Path.Combine(_databasePath, indexName + ".db");
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
		/// <param name="dateModifiedStart">The start date for when the item was last modified.</param>
		/// <param name="dateModifiedEnd">The end date for when the item was last modified.</param>
		/// <param name="fileExtension">The file extension of the item. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="sizeMin">The minimum size of the item, in bytes.</param>
		/// <param name="sizeMax">The maximum size of the item, in bytes.</param>
		/// <param name="author">The author of the document. Applicable to documents that store this metadata. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="title">The title of the document. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="comment">Any comment associated with the file. Uses CONTAINS in the query for partial matching.</param>
		/// <param name="dateCreatedStart">The start date for when the item was created.</param>
		/// <param name="dateCreatedEnd">The end date for when the item was created.</param>
		/// <param name="dateAccessedStart">The start date for when the item was last accessed.</param>
		/// <param name="dateAccessedEnd">The end date for when the item was last accessed.</param>
		/// <returns>A list of <see cref="IndexedFileInfo"/> objects that match the specified search criteria.</returns>
		/// <remarks>
		/// This method leverages the Windows Search Index, offering a powerful search functionality across various file properties.
		/// For properties like dates and size, where partial matching isn't applicable, the method uses SQL expressions to define ranges.
		/// </remarks>
		// <param name="keywords">The keywords associated with the file. This can be a collection of keywords. Each keyword uses CONTAINS in the query for partial matching.</param>
		[RiskLevel(RiskLevel.Medium)]
		public static List<IndexedFileInfo> SearchWindowsIndex(
			string contents = null,
			string itemName = null, string itemPath = null, string itemTypeText = null,
			DateTime? dateModifiedStart = null, DateTime? dateModifiedEnd = null,
			string fileExtension = null, long? sizeMin = null, long? sizeMax = null,
			string author = null, string title = null,
			//string[] keywords = null,
			string comment = null, DateTime? dateCreatedStart = null, DateTime? dateCreatedEnd = null,
			DateTime? dateAccessedStart = null, DateTime? dateAccessedEnd = null)
		{
			var whereClauses = new List<string>();
			var parameters = new List<OleDbParameter>();

			if (!string.IsNullOrEmpty(contents))
			{
				whereClauses.Add("CONTAINS(System.Search.Contents, @Contents)");
				parameters.Add(new OleDbParameter("@Content", $"\"*{contents}*\""));
			}

			if (!string.IsNullOrEmpty(itemName))
			{
				whereClauses.Add("CONTAINS(System.Search.ItemName, @ItemName)");
				parameters.Add(new OleDbParameter("@ItemName", $"\"*{itemName}*\""));
			}

			if (!string.IsNullOrEmpty(itemPath))
			{
				whereClauses.Add("CONTAINS(System.Search.ItemPath, @ItemPath)");
				parameters.Add(new OleDbParameter("@ItemPath", $"\"*{itemPath}*\""));
			}

			if (!string.IsNullOrEmpty(itemTypeText))
			{
				whereClauses.Add("CONTAINS(System.Search.ItemTypeText, @ItemTypeText)");
				parameters.Add(new OleDbParameter("@ItemTypeText", $"\"*{itemTypeText}*\""));
			}

			if (!string.IsNullOrEmpty(fileExtension))
			{
				whereClauses.Add("CONTAINS(System.Search.FileExtension, @FileExtension)");
				parameters.Add(new OleDbParameter("@FileExtension", $"\"*{fileExtension}*\""));
			}

			if (!string.IsNullOrEmpty(title))
			{
				whereClauses.Add("CONTAINS(System.Search.Title, @Title)");
				parameters.Add(new OleDbParameter("@Title", $"\"*{title}*\""));
			}

			//foreach (var keyword in keywords)
			//{
			//	if (!string.IsNullOrEmpty(keyword))
			//	{
			//		whereClauses.Add("CONTAINS(System.Search.Keywords, @Keyword)");
			//		parameters.Add(new OleDbParameter("@Keyword", $"\"*{keyword}*\""));
			//	}
			//}

			if (!string.IsNullOrEmpty(comment))
			{
				whereClauses.Add("CONTAINS(System.Search.Comment, @Comment)");
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
            SELECT System.ItemName, System.ItemPathDisplay, System.ItemTypeText, System.DateModified, 
                System.FileExtension, System.Size, System.Author, System.Title, System.DateCreated, 
                System.DateAccessed
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
				var command = new OleDbCommand(query, connection);

				foreach (var parameter in parameters)
				{
					command.Parameters.Add(parameter);
				}

				try
				{
					connection.Open();
					var reader = command.ExecuteReader();

					while (reader.Read())
					{
						var fileInfo = new IndexedFileInfo
						{
							// Populate the object fields as before.
						};
						results.Add(fileInfo);
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

	}

}
