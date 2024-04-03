using System.Data.Common;
using System;
using Embeddings;
using System.Threading.Tasks;
using System.Data;
using Embeddings.Embedding;
using System.Collections.Generic;
using JocysCom.VS.AiCompanion.DataFunctions;
using System.Linq;

#if NETFRAMEWORK
using System.Data.SQLite;
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
#endif

namespace JocysCom.VS.AiCompanion.DataClient
{
	public class SqlInitHelper
	{

		public static bool IsPortable(string connectionStringOrPath)
		{
			return connectionStringOrPath?.IndexOf(".db", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static void InitSqlDatabase(string connectionString)
		{
			if (IsPortable(connectionString))
				InitSqlLiteDatabase(connectionString);
		}


		public static void InitSqlLiteDatabase(string connectionString)
		{
#if NETFRAMEWORK
			var isPortable = IsPortable(connectionString);
			if (isPortable)
			{
				var path = ConnectionStringToPath(connectionString);
				if (!System.IO.File.Exists(path))
					SQLiteConnection.CreateFile(path);
			}
#endif
			var connection = NewConnection(connectionString);
			connection.Open();
			CreateTable(nameof(File), connection);
			CreateTable(nameof(FilePart), connection);
			connection.Close();
		}

		public static bool CreateTable(string name, DbConnection connection)
		{
			if (TableExists(name, connection))
				return false;
			var commandText = ResourceHelper.FindResource($"{name}.Sqlite.sql");
			var command = NewCommand(commandText, connection);
			command.ExecuteNonQuery();
			return true;
		}

		public static bool TableExists(string name, DbConnection connection)
		{
			string commandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name=@name;";
			var command = NewCommand(commandText, connection);
			var nameParam = command.CreateParameter();
			nameParam.ParameterName = "@name";
			nameParam.Value = name;
			command.Parameters.Add(nameParam);
			var result = command.ExecuteScalar();
			var exists = result?.ToString() == name;
			return exists;
		}

		#region Helper Methods

		public static string PathToConnectionString(string path)
		{
#if NETFRAMEWORK
			var connectionString = new SQLiteConnectionStringBuilder { DataSource = path };
#else
			var connectionString = new SqliteConnectionStringBuilder { DataSource = path };
#endif
			return connectionString.ToString();
		}

		public static string ConnectionStringToPath(string connectionString)
		{
#if NETFRAMEWORK
			var connection = new SQLiteConnectionStringBuilder(connectionString);
#else
			var connection = new SqliteConnectionStringBuilder(connectionString);
#endif
			return connection.DataSource;
		}

		public static DbConnection NewConnection(string connectionString)
		{
#if NETFRAMEWORK
			var connection = new SQLiteConnection(connectionString);
#else
			var connection = new SqliteConnection(connectionString);
#endif
			return connection;
		}

		public static DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString)
		{
#if NETFRAMEWORK
			var builder = new SQLiteConnectionStringBuilder(connectionString);
#else
			var builder = new SqliteConnectionStringBuilder(connectionString);
#endif
			return builder;
		}

		public static DbCommand NewCommand(string commandText = null, DbConnection connection = null)
		{
#if NETFRAMEWORK
			var command = new SQLiteCommand(commandText, (SQLiteConnection)connection);
#else
			var command = new SqliteCommand(commandText, (SqliteConnection)connection);
#endif
			return command;
		}

		#endregion

		public static async Task<int> SetFileState(
	EmbeddingsContext db,
	string groupName,
	EmbeddingGroup groupFlag,
	int state
)
		{
#if NETFRAMEWORK
			var connection = db.Database.Connection;
#else
			var connection = db.Database.GetDbConnection();
#endif
			var command = connection.CreateCommand();
			if (connection.State != ConnectionState.Open)
				connection.Open();
			AddParameters(command, groupName, groupFlag, state);
			var isPortable = IsPortable(connection.ConnectionString);
			var schema = isPortable ? "" : "[Embedding].";
			command.CommandText = $@"
                UPDATE {schema}[FilePart]
				SET [State] = @State
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag";
			var rowsAffected = await command.ExecuteNonQueryAsync();
			command.CommandText = $@"
                UPDATE {schema}[File]
				SET [State] = @State
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag";
			rowsAffected += await command.ExecuteNonQueryAsync();
			return rowsAffected;
		}

		public static async Task<int> DeleteByState(
			EmbeddingsContext db,
			string groupName,
			EmbeddingGroup groupFlag,
			int state
		)
		{
#if NETFRAMEWORK
			var connection = db.Database.Connection;
#else
			var connection = db.Database.GetDbConnection();
#endif
			var command = connection.CreateCommand();
			if (connection.State != ConnectionState.Open)
				connection.Open();
			AddParameters(command, groupName, groupFlag, state);
			var isPortable = IsPortable(connection.ConnectionString);
			var schema = isPortable ? "" : "[Embedding].";
			command.CommandText = $@"
                DELETE FROM {schema}[FilePart]
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag
				AND [State] = @State";
			var rowsAffected = await command.ExecuteNonQueryAsync();
			command.CommandText = $@"
                DELETE FROM {schema}[File]
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag
				AND [State] = @State";
			rowsAffected += await command.ExecuteNonQueryAsync();
			return rowsAffected;
		}

		private static void AddParameters(DbCommand command, string groupName, EmbeddingGroup groupFlag, int? state = null)
		{
			var nameParam = command.CreateParameter();
			nameParam.ParameterName = "@GroupName";
			nameParam.Value = groupName;
			command.Parameters.Add(nameParam);
			var flagParam = command.CreateParameter();
			flagParam.ParameterName = "@GroupFlag";
			flagParam.Value = (int)groupFlag;
			command.Parameters.Add(flagParam);
			if (state != null)
			{
				var stateParam = command.CreateParameter();
				stateParam.ParameterName = "@State";
				stateParam.Value = (int)state;
				command.Parameters.Add(stateParam);
			}
		}

		public static async Task<List<long>> GetSimilarFileEmbeddings(
	string connectionString,
	string groupName,
	EmbeddingGroup groupFlag,
	float[] promptVectors, int take)
		{
			var commandText = $@"
                SELECT
					fp.Id,
					fp.FileId,
					fp.Embedding
                FROM FilePart AS fp
                JOIN File AS f ON f.Id = fp.FileId
                WHERE f.GroupName = @GroupName
                AND fp.GroupFlag & @GroupFlag > 0
                AND fp.IsEnabled = 1
                AND f.IsEnabled = 1";
			var connection = NewConnection(connectionString);
			var command = NewCommand(commandText, connection);
			AddParameters(command, groupName, groupFlag);
			connection.Open();
			var reader = await command.ExecuteReaderAsync();
			var tempResult = new SortedList<float, FilePart>();
			while (await reader.ReadAsync())
			{
				var filePart = ReadFilePartFromReader(reader);
				var partVectors = EmbeddingBase.BinaryToVector(filePart.Embedding);
				var similarity = EmbeddingBase._CosineSimilarity(promptVectors, partVectors);
				// If take list is not filled yet then add and continue.
				if (tempResult.Count < take)
				{
					tempResult.Add(similarity, filePart);
					continue;
				}
				// If similarity less or same then skip and continue.
				if (similarity <= tempResult.Keys[0])
					continue;
				// Replace least similar item with the more similar.
				tempResult.RemoveAt(0);
				tempResult.Add(similarity, filePart);
			}
			var ids = tempResult
				.ToList()
				.OrderByDescending(x => x.Key)
				.Select(x => x.Value.Id)
				.ToList();
			return ids;
		}

		private static FilePart ReadFilePartFromReader(DbDataReader reader)
		{
			var filePart = new FilePart
			{
				Id = reader.GetInt64(reader.GetOrdinal("Id")),
				//GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
				//GroupFlag = reader.GetInt64(reader.GetOrdinal("GroupFlag")),
				FileId = reader.GetInt64(reader.GetOrdinal("FileId")),
				//Index = reader.GetInt32(reader.GetOrdinal("Index")),
				//Count = reader.GetInt32(reader.GetOrdinal("Count")),
				//HashType = reader.GetString(reader.GetOrdinal("HashType")),
				//Hash = (byte[])reader["Hash"],
				//Text = reader.GetString(reader.GetOrdinal("Text")),
				//TextTokens = reader.GetInt64(reader.GetOrdinal("TextTokens")),
				//EmbeddingModel = reader.GetString(reader.GetOrdinal("EmbeddingModel")),
				//EmbeddingSize = reader.GetInt32(reader.GetOrdinal("EmbeddingSize")),
				Embedding = (byte[])reader["Embedding"],
				//IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
				//Created = reader.GetDateTime(reader.GetOrdinal("Created")),
				//Modified = reader.GetDateTime(reader.GetOrdinal("Modified"))
			};
			return filePart;
		}



	}
}
