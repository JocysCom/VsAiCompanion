using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using LiteDB;
using System;
using System.Collections.Generic;
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

	}

}
