using System.Data.Common;
using System;
using Embeddings;
using System.Threading.Tasks;
using System.Data;
using Embeddings.Embedding;
using System.Collections.Generic;
using JocysCom.VS.AiCompanion.DataFunctions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

#if NETFRAMEWORK
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Entity;
using System.Data.SQLite;
// using System.Data.SQLite.Linq;
using System.Data.SQLite.EF6;
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
#endif

/* Entity Frameworks and database clients.

	1.Build-In Entity Framework 1.0 - 5.x for .NET Framework 4.8
	    - `System.Data.Entity` namespace
      Database Clients:
        - `System.Data.SqlClinet` namespace
	    - `System.Data.SQLite` package

	2. Entity Framework 6.0 (EF6) package for .NET Framework 4.8
	     - `EntityFramework` package
       Database Clients:
         - `System.Data.SqlClinet` namespace
         - `System.Data.SQLite` package
         - `System.Data.SQLite.EF6` package

	3. Entity Framework Core 7.0+ (EF Core) package for .NET Core
	     - `Microsoft.EntityFrameworkCore` package
	  Data Clients:
		  - `Microsoft.EntityFrameworkCore.Sqlite` package
	      - `Microsoft.EntityFrameworkCore.SqlServer` package
 
 */

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
			var isPortable = IsPortable(connection.DataSource);
			var suffix = isPortable
				? ".Sqlite"
				: "";
			var sqlScript = ResourceHelper.FindResource($"{name}{suffix}.sql");
			string pattern = @"^\s*GO\s*$";
			// Split the script using the Regex.Split function, considering the pattern.
			string[] commandTexts = Regex.Split(sqlScript, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			for (int i = 0; i < commandTexts.Length; i++)
			{
				var commandText = commandTexts[i];
				var command = NewCommand(commandText, connection);
				command.ExecuteNonQuery();
			}
			return true;
		}

		public static bool TableExists(string name, DbConnection connection)
		{
			var isPortable = IsPortable(connection.DataSource);
			var commandText = isPortable
				? $"SELECT name FROM sqlite_master WHERE type='table' AND name=@name;"
				: $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'Embedding' AND TABLE_NAME = @name;";
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

		public static EmbeddingsContext NewEmbeddingsContext(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
#if NETFRAMEWORK
			var connection = isPortable
				? System.Data.SQLite.EF6.SQLiteProviderFactory.Instance.CreateConnection()
				: new SqlConnection();
			connection.ConnectionString = connectionString;
			var db = new EmbeddingsContext(connection, true);
			//var db = new EmbeddingsContext();


			//Microsoft.Data.SqlClient.SqlClientFactory;
			//db.Database.Connection.ConnectionString = connectionString;
#else
			var optionsBuilder = new DbContextOptionsBuilder<EmbeddingsContext>();
			if (isPortable)
				optionsBuilder.UseSqlite(connectionString);
			else
				optionsBuilder.UseSqlServer(connectionString);
			var db = new EmbeddingsContext(optionsBuilder.Options);
#endif
			return db;
		}

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
			var isPortable = IsPortable(connectionString);
			if (!isPortable)
				return new SqlConnection(connectionString);
#if NETFRAMEWORK
			return new SQLiteConnection(connectionString);
#else
			return new SqliteConnection(connectionString);
#endif
		}

		public static DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
			if (!isPortable)
				return new SqlConnectionStringBuilder(connectionString);
#if NETFRAMEWORK
			return new SQLiteConnectionStringBuilder(connectionString);
#else
			return new SqliteConnectionStringBuilder(connectionString);
#endif
		}

		public static DbCommand NewCommand(string commandText = null, DbConnection connection = null)
		{
			var isPortable = IsPortable(connection.ConnectionString);
			if (!isPortable)
				return new SqlCommand(commandText, (SqlConnection)connection);
#if NETFRAMEWORK
			return new SQLiteCommand(commandText, (SQLiteConnection)connection);
#else
			return new SqliteCommand(commandText, (SqliteConnection)connection);
#endif
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

		public static async Task<List<Guid>> GetSimilarFileEmbeddings(
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
				Id = reader.GetGuid(reader.GetOrdinal("Id")),
				//GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
				//GroupFlag = reader.GetInt64(reader.GetOrdinal("GroupFlag")),
				FileId = reader.GetGuid(reader.GetOrdinal("FileId")),
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

		/// <summary>
		/// Convert embedding vectors to byte array.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <returns>Byte array.</returns>
		public static byte[] VectorToBinary(float[] vectors)
		{
			var bytes = new byte[vectors.Length * sizeof(float)];
			Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
			return bytes;
		}


		private static void AddFactory(DbProviderFactory instance)
		{
			var type = instance.GetType();
			var invariantName = type.FullName;
			var shortName = type.Namespace.Split('.').Last();
#if NETFRAMEWORK
			var table = DbProviderFactories.GetFactoryClasses();
			var row = table.Rows.Cast<DataRow>()
				.FirstOrDefault(x => (string)x["InvariantName"] == invariantName);
			if (row == null)
			{
				// Columns
				// [0] Name
				// [1] Description
				// [2] InvariantName
				// [3] AssemblyQualifiedName
				table.Rows.Add(
				$"{shortName} Data Provider",
				$".NET Framework Data Provider for {shortName}",
				invariantName,
				type.AssemblyQualifiedName
		   );
			}
#else
			if (!DbProviderFactories.GetProviderInvariantNames().Contains(invariantName))
				DbProviderFactories.RegisterFactory(invariantName, instance);
#endif
		}

		/// <summary>
		/// Make DbContext support SQL Server and SQLite.
		/// </summary>
		public static void AddDbProviderFactories()
		{
#if NETFRAMEWORK
			//AddFactory(Microsoft.Data.SqlClient.SqlClientFactory.Instance);
			//RegisterSqlLiteFactory();
			//RegisterSQLiteProviderServices();
			//AddFactory(System.Data.SQLite.EF6.SQLiteProviderFactory.Instance);
#else
			// Workaround fix for System.Runtime.ExceptionServices.FirstChanceException
			// The specified invariant name 'System.Data.SqlClient' wasn't found in the list of registered .NET Data Providers.
			AddFactory(SqlClientFactory.Instance);
			AddFactory(SqliteFactory.Instance);
#endif
		}

#if NETFRAMEWORK
		private static void RegisterSQLiteProviderServices()
		{
			// This method should contain the registration logic for SQLite,
			// similar to what was in MyDbConfiguration but translated to a context-based initialization
			var instance = SQLiteProviderFactory.Instance;
			var service = (System.Data.Entity.Core.Common.DbProviderServices)instance.GetService(typeof(System.Data.Entity.Core.Common.DbProviderServices));
			var instanceResolver = (IDbDependencyResolver)new SingletonDependencyResolver<DbProviderFactory>(instance, "System.Data.SQLite.EF6");
			var serviceResolver = new SingletonDependencyResolver<System.Data.Entity.Core.Common.DbProviderServices>(service, "System.Data.SQLite.EF6");
			DbConfiguration.Loaded += (_, a) =>
			{
				a.AddDependencyResolver(instanceResolver, true);
				a.AddDependencyResolver(serviceResolver, true);
			};
		}

		/*
		 * 
				<entityFramework>
		  <providers>
			<provider invariantName="System.Data.SqlClient" 
			  type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer"/>
			<provider invariantName="System.Data.SQLite.EF6" 
			  type="System.Data.SQLite.EF6.SQLiteProviderServices, System.Data.SQLite.EF6"/>
		  </providers>
		</entityFramework>

		<system.data>
		  <DbProviderFactories>
			<remove invariant="System.Data.SQLite.EF6" />
			<add name="SQLite Data Provider"
			   invariant="System.Data.SQLite.EF6"
			   description=".NET Framework Data Provider for SQLite"
			   type="System.Data.SQLite.SQLiteFactory, System.Data.SQLite" />
		  </DbProviderFactories>
		</system.data>

		*/

		public static void RegisterSqlLiteFactory()
		{
			var table = GetProviderTable();
			// Ensure any existing registrations are removed to avoid duplicate rows
			var row = table.Select("InvariantName='Microsoft.Data.SqlClient'").FirstOrDefault();
			row?.Delete();

			// Add the new DataRow with the Microsoft.Data.SqlClient.SqlClientFactory details
			table.Rows.Add(
				  "SQLite Data Provider",
				  ".NET Framework Data Provider for SQLite",
				  "System.Data.SQLite.EF6",
				  "System.Data.SQLite.SQLiteFactory, System.Data.SQLite"
			  );

			//	// Add the new DataRow with the Microsoft.Data.SqlClient.SqlClientFactory details
			//	table.Rows.Add(
			//		"Microsoft Data Provider for SQL Server",
			//		".NET Framework Data Provider for SQL Server",
			//		"Microsoft.Data.SqlClient",
			//		typeof(Microsoft.Data.SqlClient.SqlClientFactory).AssemblyQualifiedName
			//	);
			//}

		}

		private static DataTable GetProviderTable()
		{
			var factoryType = typeof(DbProviderFactories);
			var method = factoryType.GetMethod("GetProviderTable", BindingFlags.Static | BindingFlags.NonPublic);
			var table = (DataTable)method.Invoke(null, new object[] { });
			return table;
		}

#endif

	}
}
