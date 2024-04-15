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
using JocysCom.VS.AiCompanion.DataClient.Common;



#if NETFRAMEWORK
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Entity;
using System.Data.SQLite;
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

		public const string SqliteExt = ".sqlite";
		public static string[] PortableExt = new string[] { ".sqlite", ".sqlite3", ".db", ".db3", ".s3db", ".sl3" };

		public static bool IsPortable(string connectionStringOrPath)
			=> PortableExt.Any(x => connectionStringOrPath?.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);

		public static bool InitSqlDatabase(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
#if NETFRAMEWORK
			if (isPortable)
			{
				var path = ConnectionStringToPath(connectionString);
				if (!System.IO.File.Exists(path))
					SQLiteConnection.CreateFile(path);
			}
#endif
			var connection = NewConnection(connectionString);
			// Empty file will be created at this point if not exists.
			connection.Open();
			var success = true;
			if (!isPortable)
			{
				success &= CreateSchema("Embedding", connection);
				success &= CreateAssembly("DataFunctions", connection);
				success &= CreateFunction("CosineSimilarity", connection);
			}
			success &= CreateTable(nameof(File), connection);
			success &= CreateTable(nameof(FilePart), connection);
			success &= CreateTable(nameof(Embeddings.Embedding.Group), connection);
			if (!isPortable)
			{
				success &= CreateProcedure("sp_getMostSimilarFiles", connection);
				success &= CreateProcedure("sp_getSimilarFileParts", connection);
				success &= CreateProcedure("sp_getSimilarFiles", connection);
			}
			connection.Close();
			return success;
		}

		public static bool CreateTable(string name, DbConnection connection)
		{
			var isPortable = IsPortable(connection.ConnectionString);
			var commandText = isPortable
				? $"SELECT name FROM sqlite_master WHERE type='table' AND name=@name"
				: $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'Embedding' AND TABLE_NAME = @name";
			var exist = Exist(commandText, name, connection);
			return exist || RunScript(name, connection);
		}

		public static bool CreateProcedure(string name, DbConnection connection)
		{
			var commandText = $"SELECT [name] FROM sys.objects WHERE object_id = OBJECT_ID(N'[Embedding].' + QUOTENAME(@name)) AND [type] IN (N'P', N'PC')";
			var exist = Exist(commandText, name, connection);
			return exist || RunScript(name, connection);
		}

		public static bool CreateSchema(string name, DbConnection connection)
		{
			var commandText = $"SELECT [name] FROM sys.schemas WHERE name = @name";
			var exist = Exist(commandText, name, connection);
			return exist || RunScript(name, connection);
		}

		public static bool CreateFunction(string name, DbConnection connection)
		{
			var commandText = $"SELECT [name] FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].' + QUOTENAME(@name)) AND [type] IN (N'FN', N'IF', N'TF', N'FS', N'FT')";
			var exist = Exist(commandText, name, connection);
			return exist || RunScript(name, connection);
		}

		public static bool CreateAssembly(string name, DbConnection connection)
		{
			var commandText = $"SELECT [name] FROM sys.assemblies WHERE name = @name";
			var exist = Exist(commandText, name, connection);
			if (exist)
				return true;
			var success = true;
			success &= RunScript("Script.PreDeployment", connection);
			success &= RunScript(name, connection);
			success &= RunScript("Script.PostDeployment", connection);
			return success;
		}

		public static bool Exist(string commandText, string name, DbConnection connection)
		{
			var command = NewCommand(commandText, connection);
			AddParameter(command, "@name", name);
			var result = command.ExecuteScalar();
			var exists = result?.ToString() == name;
			return exists;
		}

		public static bool RunScript(string name, DbConnection connection)
		{
			var isPortable = IsPortable(connection.ConnectionString);
			var dbType = isPortable
				? "SQLite"
				: "MSSQL";
			var sqlScript = ResourceHelper.FindResource($"Setup.{dbType}.{name}.sql").Trim();
			string pattern = @"^\s*GO\s*$";
			// Split the script using the Regex.Split function, considering the pattern.
			string[] commandTexts = Regex.Split(sqlScript, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			for (int i = 0; i < commandTexts.Length; i++)
			{
				var commandText = commandTexts[i];
				if (string.IsNullOrWhiteSpace(commandText))
					continue;
				var command = NewCommand(commandText, connection);
				command.ExecuteNonQuery();
			}
			return true;
		}

		#region Helper Methods

		public static EmbeddingsContext NewEmbeddingsContext(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
#if NETFRAMEWORK
			if (isPortable)
			{
				var sconn = (System.Data.SQLite.SQLiteConnection)System.Data.SQLite.EF6.SQLiteProviderFactory.Instance.CreateConnection();
			}

			// Disable check for code first. ` __MigrationHistory` table.
			Database.SetInitializer<EmbeddingsContext>(null);
			EmbeddingsContext db;
			if (isPortable)
			{
				var connection = System.Data.SQLite.EF6.SQLiteProviderFactory.Instance.CreateConnection();
				connection.ConnectionString = connectionString;
				db = new EmbeddingsContext(connection, true);
			}
			else
			{
				var connection = new System.Data.SqlClient.SqlConnection();
				connection.ConnectionString = connectionString;
				db = new EmbeddingsContext(connection, true);
				//db = new EmbeddingsContext();
				//db.Database.Connection.ConnectionString = connectionString;
			}
#else
			var optionsBuilder = new DbContextOptionsBuilder<EmbeddingsContext>();
			if (isPortable)
				optionsBuilder.UseSqlite(connectionString);
			else
				optionsBuilder.UseSqlServer(connectionString);
#if DEBUG
			optionsBuilder.EnableSensitiveDataLogging();
#endif
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
#if NETFRAMEWORK
			if (!isPortable)
				return new System.Data.SqlClient.SqlConnection(connectionString);
			return new SQLiteConnection(connectionString);
#else
			if (!isPortable)
				return new Microsoft.Data.SqlClient.SqlConnection(connectionString);
			return new SqliteConnection(connectionString);
#endif
		}

		public static DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
#if NETFRAMEWORK
			if (!isPortable)
				return new SqlConnectionStringBuilder(connectionString);
			return new SQLiteConnectionStringBuilder(connectionString);
#else
			if (!isPortable)
				return new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
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
	EmbeddingGroupFlag groupFlag,
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
			EmbeddingGroupFlag groupFlag,
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

		private static void AddParameters(DbCommand command, string groupName, EmbeddingGroupFlag groupFlag, int? state = null)
		{
			AddParameter(command, "@GroupName", groupName);
			AddParameter(command, "@GroupFlag", (int)groupFlag);
			AddParameter(command, "@State", state);
		}

		public static async Task<List<long>> GetSimilarFileEmbeddings(
			string connectionString,
			string groupName,
			EmbeddingGroupFlag groupFlag,
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
                AND (@GroupFlag = 0 OR (fp.GroupFlag & @GroupFlag) > 0)
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

		#region Add DB Provider Factory

		// Application configuration equivalent:
		//
		//<system.data>
		//  <DbProviderFactories>
		//	<remove invariant="System.Data.SQLite.EF6" />
		//	<add name="SQLite Data Provider"
		//	   invariant="System.Data.SQLite.EF6"
		//	   description=".NET Framework Data Provider for SQLite"
		//	   type="System.Data.SQLite.SQLiteFactory, System.Data.SQLite" />
		//  </DbProviderFactories>
		//</system.data>

		private static DataTable GetDbProviderFactories()
		{
			var factoryType = typeof(DbProviderFactories);
			var method = factoryType.GetMethod("GetProviderTable", BindingFlags.Static | BindingFlags.NonPublic);
			var table = (DataTable)method.Invoke(null, new object[] { });
			return table;
		}

		/// <summary>
		/// Add Db Provider Factory.
		/// </summary>
		/// <param name="instance">Db Provider Factory instance.</param>
		public static void AddDbProviderFactory(DbProviderFactory instance)
		{
			var type = instance.GetType();
#if NETFRAMEWORK
			var invariantName = type.Namespace;
			var table = GetDbProviderFactories();
			// Ensure any existing registrations are removed to avoid duplicate rows
			var row = table.Select($"InvariantName='{invariantName}'").FirstOrDefault();
			row?.Delete();
			var rowToAdd = table.NewRow();
			rowToAdd["Name"] = $"{invariantName} Data Provider";
			rowToAdd["Description"] = $".NET Framework Data Provider for {invariantName}";
			rowToAdd["InvariantName"] = invariantName;
			rowToAdd["AssemblyQualifiedName"] = type.AssemblyQualifiedName;
			table.Rows.Add(rowToAdd);
#else
			//var invariantName = type.FullName;
			var invariantName = type.Namespace;
			if (!DbProviderFactories.GetProviderInvariantNames().Contains(invariantName))
				DbProviderFactories.RegisterFactory(invariantName, instance);
#endif
		}

		private static void ClearDbProviderFactories()
		{
			// Cleanup old providers.
			var table = GetDbProviderFactories();
			var rows = table.Rows.Cast<DataRow>().ToList();
			foreach (var row in rows)
				row?.Delete();
		}

		/// <summary>
		/// Make DbContext support SQL Server and SQLite.
		/// </summary>
		public static void AddDbProviderFactories()
		{
#if NETFRAMEWORK
			// Register the ADO.NET provider for SQLite.
			//AddDbProviderFactory(System.Data.SQLite.EF6.SQLiteProviderFactory.Instance);
			// Register the Entity Framework provider for SQLite (EF6).
			AddEntityFrameworkProviders();

#else
			// Workaround fix for System.Runtime.ExceptionServices.FirstChanceException
			// The specified invariant name 'System.Data.SqlClient' wasn't found in the list of registered .NET Data Providers.
			AddDbProviderFactory(SqlClientFactory.Instance);
			AddDbProviderFactory(SqliteFactory.Instance);
#endif
		}

		#endregion

		#region Add Entity Framework Providers

		// Application configuration equivalent:
		//
		//<entityFramework>
		//  <providers>
		//	<provider invariantName="System.Data.SqlClient" 
		//	  type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer"/>
		//	<provider invariantName="System.Data.SQLite.EF6" 
		//	  type="System.Data.SQLite.EF6.SQLiteProviderServices, System.Data.SQLite.EF6"/>
		//  </providers>
		//</entityFramework>


#if NETFRAMEWORK

		private static void AddEntityFrameworkProviders()
		{
			System.Data.Entity.DbConfiguration.Loaded += (sender, args) =>
			{
				args.AddDependencyResolver(new SqlLiteEF6Resolver(), true);
				args.AddDependencyResolver(new MsSqlEF6Resolver(), true);
			};
		}

		public class SqlLiteEF6Resolver : System.Data.Entity.Infrastructure.DependencyResolution.IDbDependencyResolver
		{

			System.Data.SQLite.EF6.SQLiteProviderFactory instance
				=> System.Data.SQLite.EF6.SQLiteProviderFactory.Instance;

			// "System.Data.SQLite.EF6"
			string invariantName
				=> instance.GetType().Namespace;

			/// <inheritdoc />
			public object GetService(Type type, object key)
			{
				if (type == typeof(System.Data.Entity.Infrastructure.IProviderInvariantName))
				{
					if (key is System.Data.SQLite.SQLiteFactory)
						return new ProviderInvariantName(invariantName);
					if (key is System.Data.SQLite.EF6.SQLiteProviderFactory)
						return new ProviderInvariantName(invariantName);
				}
				else if (type == typeof(System.Data.Common.DbProviderFactory))
				{
					if (invariantName.Equals(key))
						return instance;
				}
				else if (type == typeof(System.Data.Entity.Core.Common.DbProviderServices))
				{
					if (invariantName.Equals(key))
						return instance.GetService(type);
				}
				return null;
			}

			/// <inheritdoc />
			public IEnumerable<object> GetServices(Type type, object key)
				=> new object[] { GetService(type, key) }.Where(o => o != null);

		}

		public class MsSqlEF6Resolver : System.Data.Entity.Infrastructure.DependencyResolution.IDbDependencyResolver
		{
			System.Data.SqlClient.SqlClientFactory instance
				=> System.Data.SqlClient.SqlClientFactory.Instance;

			// "System.Data.SQLite.EF6"
			string invariantName
				=> instance.GetType().Namespace;

			/// <inheritdoc />
			public object GetService(Type type, object key)
			{
				if (type == typeof(System.Data.Entity.Infrastructure.IProviderInvariantName))
				{
					if (key is System.Data.SqlClient.SqlClientFactory)
						return new ProviderInvariantName(invariantName);
				}
				else if (type == typeof(System.Data.Common.DbProviderFactory))
				{
					if (invariantName.Equals(key))
						return instance;
				}
				else if (type == typeof(System.Data.Entity.Core.Common.DbProviderServices))
				{
					if (invariantName.Equals(key))
						return System.Data.Entity.SqlServer.SqlProviderServices.Instance;
				}
				return null;
			}

			/// <inheritdoc />
			public IEnumerable<object> GetServices(Type type, object key)
				=> new object[] { GetService(type, key) }.Where(o => o != null);

		}


		class ProviderInvariantName : System.Data.Entity.Infrastructure.IProviderInvariantName
		{
			public string Name { get; private set; }
			public ProviderInvariantName(string name)
			{
				Name = name;
			}
		}

#endif

		#endregion

	}
}
