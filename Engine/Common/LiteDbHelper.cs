using LiteDB;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class LiteDbHelper
	{
		public static void InitLiteDB(string path)
		{
			if (!File.Exists(path))
			{
				using (var db = new LiteDatabase(path))
				{
					// Create or get collection for File entities
					var filesCollection = db.GetCollection<Embeddings.Embedding.File>("Files");
					SetupFileCollectionIndexes(filesCollection);

					// Create or get collection for FilePart entities
					var filePartsCollection = db.GetCollection<Embeddings.Embedding.FilePart>("FileParts");
					SetupFilePartCollectionIndexes(filePartsCollection);
				}
			}
		}

		private static void SetupFileCollectionIndexes(ILiteCollection<Embeddings.Embedding.File> collection)
		{
			collection.EnsureIndex(x => x.Id, true); // Primary key
			collection.EnsureIndex(x => x.HashType); // Based on non-clustered index
			collection.EnsureIndex(x => x.Hash); // Based on non-clustered index
		}

		private static void SetupFilePartCollectionIndexes(ILiteCollection<Embeddings.Embedding.FilePart> collection)
		{
			collection.EnsureIndex(x => x.Id, true); // Primary key
			collection.EnsureIndex(x => x.FileId); // FileId is frequently queried and part of non-clustered indexes
			collection.EnsureIndex(x => x.HashType); // Based on non-clustered index
			collection.EnsureIndex(x => x.Hash); // Based on non-clustered index
			collection.EnsureIndex(x => x.GroupFlag); // Apart of a non-clustered index
			collection.EnsureIndex(x => x.Index); // Frequently used in queries and non-clustered index
			collection.EnsureIndex(x => x.IsEnabled); // Frequently used in queries and non-clustered index
		}
	}
}
