using System;
using System.Linq;

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

		/// <summary>
		///  Get hasg size by name.
		/// </summary>
		/// <param name="hashName"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static int GetHashSizeByName(string hashName)
		{
			switch (hashName.ToUpperInvariant())
			{
				case "MD2": // MD2 is not officially supported in C#, but it's typically 128 bits (16 bytes).
					return 16;
				case "MD4": // MD4 is also 128 bits (16 bytes).
					return 16;
				case "MD5": // MD5 hash size is 128 bits (16 bytes).
					return 16;
				case "SHA": // SHA generally refers to SHA-0 which is 160 bits (20 bytes), but it's obsolete.
				case "SHA1": // SHA1 hash size is 160 bits (20 bytes).
					return 20;
				case "SHA2_256": // SHA-256 hash size is 256 bits (32 bytes).
					return 32;
				case "SHA2_512": // SHA-512 hash size is 512 bits (64 bytes).
					return 64;
				default:
					return 64;
			}
		}

		public const string SHA2_256 = nameof(SHA2_256);

		/// <summary>
		///  Get hash by name.
		/// </summary>
		public static byte[] GetHashByName(byte[] bytes, string hashName)
		{
			if (bytes == null)
				return null;
			return bytes.Take(GetHashSizeByName(hashName)).ToArray();
		}

	}
}
