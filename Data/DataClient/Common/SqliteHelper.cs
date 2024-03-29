using System.IO;
using System.Data.Common;
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
	public class SqliteHelper
	{
		public static void InitSqlLiteDatabase(string path)
		{
#if NETFRAMEWORK
			if (!File.Exists(path))
				SQLiteConnection.CreateFile(path);
#endif
			var connection = NewConnection(path);
			connection.Open();
			CreateTable(nameof(Embeddings.Embedding.File), connection);
			CreateTable(nameof(Embeddings.Embedding.FilePart), connection);
			connection.Close();
		}

		public static bool CreateTable(string name, DbConnection connection)
		{
			if (TableExists(name, connection))
				return false;
			var commandText = ResourceHelper.FindResource($"{name}.Sqlite.sql");
			var command = NewCommand(commandText, connection);
			command.ExecuteNonQuery();
			connection.Close();
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

		public static DbConnection NewConnection(string path)
		{
#if NETFRAMEWORK
			var connectionString = new SQLiteConnectionStringBuilder { DataSource = path }.ToString();
			var connection = new SQLiteConnection(connectionString);
#else
			var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
			var connection = new SqliteConnection(connectionString);
#endif
			return connection;
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

	}
}
