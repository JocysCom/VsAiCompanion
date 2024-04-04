using System.Data;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Embeddings.Embedding;
using System.Data.Common;
using System.Threading;

#if NETFRAMEWORK
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
#else
using Microsoft.EntityFrameworkCore;
#endif

namespace Embeddings
{
	/// <summary>Embeddings Context</summary>
	public partial class EmbeddingsContext
	{

		/// <summary>
		/// Get similar file parts.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <param name="skip">Skip records.</param>
		/// <param name="take">Take records.</param>
		/// <returns>Similar file parts with the most similar on top.</returns>
		public async Task<List<FilePart>> sp_getSimilarFileParts(
			float[] vectors, int skip, int take,
			CancellationToken cancellationToken = default
			)
		{
			var connection = GetConnection();
			var command = connection.CreateCommand();
			command.CommandText = "EXEC [Embedding].[sp_getSimilarFileParts] @vectors, @skip, @take";
			command.CommandType = CommandType.StoredProcedure;
			var vectorsValue = VectorToBinary(vectors);
			AddParameter(command, "@vectors", vectorsValue);
			AddParameter(command, "@skip", skip);
			AddParameter(command, "@take", take);
			var reader = await command.ExecuteReaderAsync(cancellationToken);
			var items = reader.Cast<FilePart>().ToList();
			return items;
		}

		public DbConnection GetConnection()
		{
#if NETFRAMEWORK
			return Database.Connection;
#else
			return Database.GetDbConnection();
#endif
		}

		private static DbParameter AddParameter(DbCommand command, string parameterName, object value)
		{
			if (value is null)
				return null;
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Value = value;
			command.Parameters.Add(parameter);
			return parameter;
		}


		/// Convert embedding vectors to byte array.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <returns>Byte array.</returns>
		static byte[] VectorToBinary(float[] vectors)
		{
			byte[] bytes = new byte[vectors.Length * sizeof(float)];
			Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
			return bytes;
		}

	}
}
