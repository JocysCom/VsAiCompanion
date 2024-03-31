using System;

namespace JocysCom.VS.AiCompanion.DataFunctions
{
	/// <summary>
	/// AI embedding functions.
	/// </summary>
	public static partial class EmbeddingBase
	{
		/// <summary>
		/// Convert byte array to embedding vectors.
		/// </summary>
		/// <param name="bytes">Bytes.</param>
		/// <returns>Float array.</returns>
		public static float[] BinaryToVector(byte[] bytes)
		{
			float[] floats = new float[bytes.Length / sizeof(float)];
			Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
			return floats;
		}

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

		/// <summary>
		/// Compare two embedding vectors using cosine similarity.
		/// </summary>
		/// <param name="vectors1">Embedding vectors 1.</param>
		/// <param name="vectors2">Embedding vectors 2.</param>
		/// <returns>Similarity value.</returns>
		public static float _CosineSimilarity(float[] vectors1, float[] vectors2)
		{
			float dotProduct = 0;
			float norm1 = 0, norm2 = 0;
			for (int i = 0; i < vectors1.Length; i++)
			{
				dotProduct += vectors1[i] * vectors2[i];
				norm1 += vectors1[i] * vectors1[i];
				norm2 += vectors2[i] * vectors2[i];
			}
			return dotProduct / ((float)Math.Sqrt(norm1) * (float)Math.Sqrt(norm2));
		}

	}
}
