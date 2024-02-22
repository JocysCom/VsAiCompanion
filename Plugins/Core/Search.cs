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
		public string _databasePath;

		/// <summary>
		/// Index a specific folder.
		/// </summary>
		/// <param name="indexName">Index name.</param>
		/// <param name="folderPath">Folder path.</param>
		[RiskLevel(RiskLevel.Low)]
		public void IndexFolder(string indexName, string folderPath)
		{
			using (var db = new LiteDatabase(_databasePath))
			{
				var filesCollection = db.GetCollection<FileData>(indexName);

				// Ensure we have an index on the Path field
				filesCollection.EnsureIndex(x => x.Path);

				foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
				{
					var fileInfo = new FileInfo(filePath);

					// Insert or update the file document in the LiteDB collection
					var fileData = new FileData
					{
						Path = fileInfo.FullName,
						Name = fileInfo.Name,
						Size = fileInfo.Length,
						LastModified = fileInfo.LastWriteTimeUtc
					};

					filesCollection.Upsert(fileData);
				}
			}
		}

		/// <summary>
		/// Search an index and return search results.
		/// </summary>
		/// <param name="indexName">Index name.</param>
		/// <param name="searchString">Search string.</param>
		[RiskLevel(RiskLevel.Low)]
		public List<string> SearchIndex(string indexName, string searchString)
		{
			using (var db = new LiteDatabase(_databasePath))
			{
				var filesCollection = db.GetCollection<FileData>(indexName);

				// Perform the search using a simple string Contains query
				var results = filesCollection.Find(x => x.Name.Contains(searchString));

				// Print out the results
				foreach (var file in results)
				{
					Console.WriteLine($"Found: {file.Path}");
				}
				return new List<string>(results.Select(x => x.Path));
			}
		}

		// A simple POCO (Plain Old CLR Object) to represent a file's data
		private class FileData
		{
			public int Id { get; set; }
			public string Path { get; set; }
			public string Name { get; set; }
			public long Size { get; set; }
			public DateTime LastModified { get; set; }
		}
	}

}
