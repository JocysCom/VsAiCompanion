using Microsoft.SqlServer.Server;

namespace JocysCom.VS.AiCompanion.DataFunctions
{
	/// <summary>
	/// AI embedding functions.
	/// </summary>
	public static partial class EmbeddingBase
	{
		/// <summary>
		/// Compare two embedding vectors using cosine similarity.
		/// </summary>
		/// <param name="vector1">Embedding vector 1.</param>
		/// <param name="vector2">Embedding vector 2.</param>
		/// <returns>Similarity value.</returns
		[SqlFunction]
		public static float CosineSimilarity(byte[] bytes1, byte[] bytes2)
		{
			var vectors1 = BinaryToVector(bytes1);
			var vectors2 = BinaryToVector(bytes2);
			return _CosineSimilarity(vectors1, vectors2);
		}

	}
}
