using Microsoft.SqlServer.Server;
using System;

namespace JocysCom.VS.AiCompanion.DataFunctions
{
    /// <summary>
    /// AI embedding functions.
    /// </summary>
    public static class EmbeddingBase
    {
        /// <summary>
        /// Convert byte array to embedding vectors.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>Float array.</returns>
        [SqlFunction]
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
        [SqlFunction]
        public static byte[] VectorToBinary(float[] vectors)
        {
            byte[] bytes = new byte[vectors.Length * sizeof(float)];
            Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Compare two embedding vectors using cosine similarity.
        /// </summary>
        /// <param name="vector1">Embedding vector 1.</param>
        /// <param name="vector2">Embedding vector 2.</param>
        /// <returns>Similarity value.</returns>
        [SqlFunction]
        public static float CosineSimilarity(float[] vector1, float[] vector2)
        {
            float dotProduct = 0;
            float norm1 = 0, norm2 = 0;
            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                norm1 += vector1[i] * vector1[i];
                norm2 += vector2[i] * vector2[i];
            }
            return dotProduct / ((float)Math.Sqrt(norm1) * (float)Math.Sqrt(norm2));
        }

    }
}
