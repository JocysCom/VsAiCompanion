﻿using System.Data.Common;
using System;
using Embeddings;
using System.Threading.Tasks;
using System.Data;
using Embeddings.Embedding;
using System.Collections.Generic;
using JocysCom.VS.AiCompanion.DataFunctions;
using System.Linq;
using System.Reflection;
using JocysCom.VS.AiCompanion.DataClient.Common;
using System.Threading;
using SQLitePCL;
using Microsoft.Data.SqlClient;


#if NETFRAMEWORK
using System.Data.Entity;
using System.Data.SQLite;
using System.Data.Entity.SqlServer;
#else
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

		/// <summary>
		/// Requires for SQLite to work.
		/// Requires `SQLitePCLRaw.bundle_e_sqlite3` nuget package.
		/// </summary>
		public static void SqlBatteriesInit()
		{
			Batteries_V2.Init();
		}

		public const string SqliteExt = ".sqlite";
		public static string[] PortableExt = new string[] { ".sqlite", ".sqlite3", ".db", ".db3", ".s3db", ".sl3" };

		#region Microsoft Azure/Entra Support

		/// <summary>
		/// Determines if the exception was caused by an expired token.
		/// </summary>
		private static bool IsTokenExpired(SqlException ex)
		{
			// Identify if the exception is due to an expired token; implement your custom check.
			return ex.Message.Contains("token expired") || ex.Message.Contains("re-authentication");
		}

		#endregion

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
			bool success = true;
			var addCLR = !isPortable && !IsAzureSQL(null); // Passing null temporarily

			// Use 'using' statement to ensure the connection is disposed
			using (var connection = NewConnection(connectionString))
			{
				// Empty file will be created at this point if not exists.
				connection.Open();

				if (!isPortable)
				{
					success &= CreateSchema("Embedding", connection);
					if (addCLR)
					{
						//success &= CreateAssembly("DataFunctions", connection);
						//success &= CreateFunction("CosineSimilarity", connection);
					}
				}
				success &= CreateTable(nameof(Embeddings.Embedding.File), connection);
				success &= CreateTable(nameof(Embeddings.Embedding.FilePart), connection);
				success &= CreateTable(nameof(Embeddings.Embedding.Group), connection);
				success &= RunScript("Update_1", connection, isPortable);
				if (!isPortable)
				{
					//success &= CreateProcedure("sp_getMostSimilarFiles", connection);
					success &= CreateProcedure("sp_getSimilarFileParts", connection);
					//success &= CreateProcedure("sp_getSimilarFiles", connection);
				}
				// The connection will be automatically closed and disposed when exiting the using block
			}
			return success;
		}


		/// <summary>
		/// Return true if Azure SQL and don't support CLR.
		/// Managed SQL instances support CLR.
		/// </summary>
		/// <returns></returns>
		public static bool IsAzureSQL(DbConnection connection)
		{
			if (connection is null)
				return false;
			// 1 = Desktop, 2 = Standard, 3 = Enterprise, 4 = Express, 5 = SQL Azure
			var commandText = $"SELECT SERVERPROPERTY('EngineEdition')";
			var command = NewCommand(commandText, connection);
			var result = command.ExecuteScalar();
			var isAzureSQL = result?.ToString() == "5";
			return isAzureSQL;
		}

		public static bool ContainsTable(string name, DbConnection connection)
		{
			var isPortable = IsPortable(connection.ConnectionString);
			var commandText = isPortable
				? $"SELECT name FROM sqlite_master WHERE type='table' AND name=@name"
				: $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'Embedding' AND TABLE_NAME = @name";
			var exist = Exist(commandText, name, connection);
			return exist;
		}

		public static bool CreateTable(string name, DbConnection connection)
		{
			return ContainsTable(name, connection) || RunScript(name, connection);
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

		public static bool RunScript(string name, DbConnection connection, bool ignoreError = false)
		{
			var isPortable = IsPortable(connection.ConnectionString);
			var dbType = isPortable
				? "SQLite"
				: "MSSQL";
			var sqlScript = ResourceHelper.FindResource($"Setup.{dbType}.{name}.sql").Trim();
			string pattern = @"^\s*GO\s*$";
			// Split the script using the Regex.Split function, considering the pattern.
			string[] commandTexts = System.Text.RegularExpressions.Regex.Split(sqlScript, pattern,
				System.Text.RegularExpressions.RegexOptions.IgnoreCase |
				System.Text.RegularExpressions.RegexOptions.Multiline);
			for (int i = 0; i < commandTexts.Length; i++)
			{
				var commandText = commandTexts[i];
				if (string.IsNullOrWhiteSpace(commandText))
					continue;
				var command = NewCommand(commandText, connection);
				try
				{
					command.ExecuteNonQuery();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					if (!ignoreError)
						throw;
				}
			}
			return true;
		}

		#region Helper Methods

		public static EmbeddingsContext NewEmbeddingsContext(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
#if NETFRAMEWORK
			// Disable check for code first. ` __MigrationHistory` table.
			Database.SetInitializer<EmbeddingsContext>(null);
			EmbeddingsContext db;
			DbConnection connection;
			if (isPortable)
			{
				connection = NewSqliteConnection(connectionString);
			}
			else
			{
				connection = new System.Data.SqlClient.SqlConnection(connectionString);
				//connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
			}
			db = new EmbeddingsContext(connection, true);
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
			var conn = new System.Data.SQLite.SQLiteConnectionStringBuilder() { DataSource = path };
#else
			var conn = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder() { DataSource = path };
#endif
			// Ensure file is not locked.
			conn.Pooling = false;
			return conn.ToString();
		}

		private static DbConnection NewSqliteConnection(string connectionString)
		{
#if NETFRAMEWORK
			var conn = new System.Data.SQLite.SQLiteConnection(connectionString);
#else
			var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
#endif
			return conn;
		}

		public static string ConnectionStringToPath(string connectionString)
		{
#if NETFRAMEWORK
			var conn = new System.Data.SQLite.SQLiteConnectionStringBuilder(connectionString);
#else
			var conn = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
#endif
			return conn.DataSource;
		}

		public static DbConnection NewConnection(string connectionString)
		{
			var isPortable = IsPortable(connectionString);
			var conn = isPortable

				? NewSqliteConnection(connectionString)
				: (DbConnection)new Microsoft.Data.SqlClient.SqlConnection(connectionString);
#if !NETFRAMEWORK
			if (!isPortable)
			{
				var isMicrosoftCloud = connectionString.IndexOf("database.windows.net", StringComparison.OrdinalIgnoreCase) > -1;
				var activeDirectory = connectionString.IndexOf("Active Directory Default", StringComparison.OrdinalIgnoreCase) > -1;
				if (isMicrosoftCloud && !activeDirectory)
					// Not supported in visual studio extension.
					((SqlConnection)conn).AccessTokenCallback = GetAccessTokenAsync;
			}
#endif
			return conn;
		}

		public static Func<CancellationToken, Task<Azure.Core.AccessToken>> GetAzureSqlAccessToken;

		private static async Task<SqlAuthenticationToken> GetAccessTokenAsync(SqlAuthenticationParameters parameters, CancellationToken cancellationToken)
		{
			var token = await GetAzureSqlAccessToken(cancellationToken);
			return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
		}



		public static DbCommand NewCommand(string commandText = null, DbConnection connection = null)
		{
			var cmd = IsPortable(connection.ConnectionString)
#if NETFRAMEWORK
				? (DbCommand)new SQLiteCommand(commandText)
#else
				? (DbCommand)new SqliteCommand(commandText)
#endif
				: (DbCommand)new SqlCommand(commandText);
			cmd.Connection = connection;
			return cmd;
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
			if (connection.State != ConnectionState.Open)
				connection.Open();

			try
			{
				using (var command = connection.CreateCommand())
				{
					AddParameters(command, groupName, groupFlag, state);

					var isPortable = IsPortable(connection.ConnectionString);
					var schema = isPortable ? "" : "[Embedding].";
					var filePartTable = $"{schema}[{nameof(Embeddings.Embedding.FilePart)}]";
					var fileTable = $"{schema}[{nameof(Embeddings.Embedding.File)}]";
					var groupTable = $"{schema}[{nameof(Embeddings.Embedding.Group)}]";

					command.CommandText = $@"
                UPDATE {filePartTable}
                SET [State] = @State
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag";
					var rowsAffected = await command.ExecuteNonQueryAsync();

					command.CommandText = $@"
                UPDATE {fileTable}
                SET [State] = @State
                WHERE [GroupName] = @GroupName
                AND [GroupFlag] = @GroupFlag";
					rowsAffected += await command.ExecuteNonQueryAsync();

					return rowsAffected;
				}
			}
			finally
			{
				if (connection.State != ConnectionState.Closed)
					connection.Close();
			}
		}

		public static async Task<int> DeleteByState(
			EmbeddingsContext db,
			string groupName,
			EmbeddingGroupFlag? groupFlag = null,
			int? state = null
		)
		{
#if NETFRAMEWORK
			var connection = db.Database.Connection;
#else
			var connection = db.Database.GetDbConnection();
#endif
			var isConnectionOpenedHere = false;
			if (connection.State != ConnectionState.Open)
			{
				await connection.OpenAsync();
				isConnectionOpenedHere = true;
			}
			try
			{
				int rowsAffected = 0;
				using (var command = connection.CreateCommand())
				{
					AddParameters(command, groupName, groupFlag, state);
					var isPortable = IsPortable(connection.ConnectionString);
					var schema = isPortable ? "" : "[Embedding].";
					var filePartTable = $"{schema}[{nameof(Embeddings.Embedding.FilePart)}]";
					var fileTable = $"{schema}[{nameof(Embeddings.Embedding.File)}]";
					var groupTable = $"{schema}[{nameof(Embeddings.Embedding.Group)}]";
					string commandText;
					// Delete file parts.
					commandText = $"DELETE FROM {filePartTable}\r\n";
					commandText += $"WHERE [GroupName] = @GroupName\r\n";
					if (groupFlag != null)
						commandText += $"\tAND [GroupFlag] = @GroupFlag\r\n";
					if (state != null)
						commandText += $"\tAND [State] = @State\r\n";
					command.CommandText = commandText;
					rowsAffected += await command.ExecuteNonQueryAsync();
					// Delete files.
					command.CommandText = $"DELETE FROM {fileTable}\r\n";
					command.CommandText += $"WHERE [GroupName] = @GroupName\r\n";
					if (groupFlag != null)
						command.CommandText += $"\tAND [GroupFlag] = @GroupFlag\r\n";
					if (state != null)
						command.CommandText += $"\tAND [State] = @State\r\n";
					rowsAffected += await command.ExecuteNonQueryAsync();
					// Cleanup group records.
					command.CommandText = $"DELETE FROM {groupTable}\r\n";
					command.CommandText += $"WHERE [Name] NOT IN (\r\n";
					command.CommandText += $"\tSELECT DISTINCT [GroupName]\r\n";
					command.CommandText += $"\tFROM {fileTable}\r\n";
					command.CommandText += $") OR [Flag] NOT IN (\r\n";
					command.CommandText += $"\tSELECT DISTINCT [GroupFlag]\r\n";
					command.CommandText += $"\tFROM {fileTable}\r\n";
					command.CommandText += $"\tWHERE [GroupName] = {groupTable}.[Name]\r\n";
					command.CommandText += $");\r\n";
					rowsAffected += await command.ExecuteNonQueryAsync();
				}
				return rowsAffected;
			}
			finally
			{
				// Ensure the connection is closed if it was opened here
				if (isConnectionOpenedHere && connection.State != ConnectionState.Closed)
				{
					connection.Close();
				}
			}
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

		private static void AddParameters(DbCommand command, string groupName, EmbeddingGroupFlag? groupFlag = null, int? state = null)
		{
			AddParameter(command, "@GroupName", groupName);
			AddParameter(command, "@GroupFlag", (int?)groupFlag);
			AddParameter(command, "@State", state);
		}

		public static async Task<List<long>> GetSimilarFileEmbeddings(
		bool isPortable,
		string connectionString,
		string groupName,
		EmbeddingGroupFlag groupFlag,
		float[] promptVectors, int take)
		{
			var schema = isPortable ? "" : "[Embedding].";
			var filePartTable = $"{schema}[{nameof(Embeddings.Embedding.FilePart)}]";
			var fileTable = $"{schema}[{nameof(Embeddings.Embedding.File)}]";
			var groupTable = $"{schema}[{nameof(Embeddings.Embedding.Group)}]";

			var commandText =
				$@"
        SELECT
            fp.Id,
            fp.FileId,
            fp.Embedding
        FROM {filePartTable} AS fp
        JOIN {fileTable} AS f ON f.Id = fp.FileId
        WHERE (@GroupName = '' OR @GroupName = f.GroupName)
        AND (@GroupFlag = 0 OR (@GroupFlag & fp.GroupFlag) > 0)
        AND fp.IsEnabled = 1
        AND f.IsEnabled = 1";
			var tempResult = new List<(float similarity, FilePart filePart)>();
			using (var connection = NewConnection(connectionString))
			{
				var command = NewCommand(commandText, connection);
				AddParameters(command, groupName, groupFlag);
				await connection.OpenAsync();
				var reader = await command.ExecuteReaderAsync();
				while (await reader.ReadAsync())
				{
					var filePart = ReadFilePartFromReader(reader);
					var partVectors = EmbeddingBase.BinaryToVector(filePart.Embedding);
					var similarity = EmbeddingBase._CosineSimilarity(promptVectors, partVectors);
					// If take list is not filled yet then add and continue.
					if (tempResult.Count < take)
					{
						tempResult.Add((similarity, filePart));
						continue;
					}
					// Sort the list if it's at capacity to ensure the least similar item is at the beginning
					tempResult.Sort((x, y) => x.similarity.CompareTo(y.similarity));
					// If more similar found then...
					if (similarity > tempResult[0].similarity)
					{
						tempResult.RemoveAt(0);
						tempResult.Add((similarity, filePart));
					}
				}
			}
			// Final sort to order by descending similarity before extracting IDs.
			var ids = tempResult
				.OrderByDescending(x => x.similarity)
				.Select(x => x.filePart.Id)
				.ToList();
			return ids;
		}

		public static async Task<List<DataInfo>> SelectDataInfo(
		bool isPortable,
		string connectionString)
		{
			var schema = isPortable ? "" : "[Embedding].";
			var filePartTable = $"{schema}[{nameof(Embeddings.Embedding.FilePart)}]";
			var fileTable = $"{schema}[{nameof(Embeddings.Embedding.File)}]";
			var groupTable = $"{schema}[{nameof(Embeddings.Embedding.Group)}]";

			var commandText =
				$@"
			SELECT
				fp.GroupName,
				fp.GroupFlag,
				g.FlagName,
				Count(*) AS Count
			FROM {filePartTable} AS fp
			LEFT JOIN {groupTable} AS g ON g.Name = fp.GroupName AND g.Flag = fp.GroupFlag
			GROUP BY fp.GroupName, fp.GroupFlag, g.FlagName
			ORDER BY fp.GroupName, fp.GroupFlag, g.FlagName
			";
			var items = new List<DataInfo>();
			using (var connection = NewConnection(connectionString))
			{
				var command = NewCommand(commandText, connection);
				await connection.OpenAsync();
				var reader = await command.ExecuteReaderAsync();
				while (await reader.ReadAsync())
				{
					var item = new DataInfo();
					item.GroupName = reader.GetString(reader.GetOrdinal(nameof(DataInfo.GroupName)));
					item.GroupFlag = reader.GetInt64(reader.GetOrdinal(nameof(DataInfo.GroupFlag)));
					var flagNameOrdinal = reader.GetOrdinal(nameof(Group.FlagName));
					if (!reader.IsDBNull(flagNameOrdinal))
						item.GroupFlagName = reader.GetString(flagNameOrdinal);
					item.Count = reader.GetInt32(reader.GetOrdinal("Count"));
					items.Add(item);
				}
			}
			return items;
		}

		private static FilePart ReadFilePartFromReader(DbDataReader reader)
		{
			var filePart = new FilePart
			{
				Id = reader.GetInt64(reader.GetOrdinal(nameof(FilePart.Id))),
				//GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
				//GroupFlag = reader.GetInt64(reader.GetOrdinal("GroupFlag")),
				FileId = reader.GetInt64(reader.GetOrdinal(nameof(FilePart.FileId))),
				//Index = reader.GetInt32(reader.GetOrdinal("Index")),
				//Count = reader.GetInt32(reader.GetOrdinal("Count")),
				//HashType = reader.GetString(reader.GetOrdinal("HashType")),
				//Hash = (byte[])reader["Hash"],
				//Text = reader.GetString(reader.GetOrdinal("Text")),
				//TextTokens = reader.GetInt64(reader.GetOrdinal("TextTokens")),
				//EmbeddingModel = reader.GetString(reader.GetOrdinal("EmbeddingModel")),
				//EmbeddingSize = reader.GetInt32(reader.GetOrdinal("EmbeddingSize")),
				Embedding = (byte[])reader[nameof(FilePart.Embedding)],
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

#if !NETFRAMEWORK

		/// <summary>
		/// Add Db Provider Factory.
		/// </summary>
		/// <param name="instance">Db Provider Factory instance.</param>
		public static void AddDbProviderFactory(DbProviderFactory instance)
		{
			var type = instance.GetType();
			//var invariantName = type.FullName;
			var invariantName = type.Namespace;
			if (!DbProviderFactories.GetProviderInvariantNames().Contains(invariantName))
				DbProviderFactories.RegisterFactory(invariantName, instance);
		}

#endif

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
			// Setting EF6 configuration OPTION 2.
			//DbConfiguration.SetConfiguration(new MicrosoftSqlDbConfiguration());
			//DbConfiguration.SetConfiguration(new EntityFrameworkConfiguration());
			// Setting EF6 configuration OPTION 3.
			DbConfiguration.Loaded += (sender, args) =>
			{
				// Use System.Data.SqlClient and SqlLite client with EF6 in VS2022
				// There are too many problems using `Microsoft.Data.SqlClient` with EF6 inside Visual Studio 2022.
				args.AddDependencyResolver(new SystemSqlEF6Resolver(), overrideConfigFile: true);
				//args.AddDependencyResolver(new MicrosoftSqEF6Resolver(), overrideConfigFile: true);
				args.AddDependencyResolver(new SqlLiteEF6Resolver(), overrideConfigFile: true);
			};

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
					if (string.Equals(key as string, invariantName, StringComparison.OrdinalIgnoreCase))
						return instance;
				}
				else if (type == typeof(System.Data.Entity.Core.Common.DbProviderServices))
				{
					if (string.Equals(key as string, invariantName, StringComparison.OrdinalIgnoreCase))
						return instance.GetService(type);
				}
				return null;
			}

			/// <inheritdoc />
			public IEnumerable<object> GetServices(Type type, object key)
				=> new object[] { GetService(type, key) }.Where(o => o != null);

		}

		public class SystemSqlEF6Resolver : System.Data.Entity.Infrastructure.DependencyResolution.IDbDependencyResolver
		{
			private readonly System.Data.Common.DbProviderFactory _providerFactory;
			private readonly System.Data.Entity.Core.Common.DbProviderServices _providerServices;
			private readonly System.Data.Entity.Infrastructure.IProviderInvariantName _providerInvariantName;

			public SystemSqlEF6Resolver()
			{
				// Set the provider invariant name for System.Data.SqlClient
				string invariantName = "System.Data.SqlClient";
				// Initialize the provider factory and services
				_providerFactory = System.Data.SqlClient.SqlClientFactory.Instance;
				// Use the provider services for System.Data.SqlClient
				_providerServices = System.Data.Entity.SqlServer.SqlProviderServices.Instance;
				_providerInvariantName = new ProviderInvariantName(invariantName);
			}

			/// <inheritdoc />
			public object GetService(Type type, object key)
			{
				if (type == typeof(System.Data.Entity.Infrastructure.IProviderInvariantName))
				{
					if (key == _providerFactory)
						return _providerInvariantName;
				}
				else if (type == typeof(System.Data.Common.DbProviderFactory))
				{
					if (string.Equals(key as string, _providerInvariantName.Name, StringComparison.OrdinalIgnoreCase))
						return _providerFactory;
				}
				else if (type == typeof(System.Data.Entity.Core.Common.DbProviderServices))
				{
					if (string.Equals(key as string, _providerInvariantName.Name, StringComparison.OrdinalIgnoreCase))
						return _providerServices;
				}
				else if (type == typeof(System.Data.Entity.Infrastructure.IDbExecutionStrategy))
				{
					if (string.Equals(key as string, _providerInvariantName.Name, StringComparison.OrdinalIgnoreCase))
						return new SqlAzureExecutionStrategy();
				}
				return null;
			}

			/// <inheritdoc />
			public IEnumerable<object> GetServices(Type type, object key)
				=> new[] { GetService(type, key) }.Where(o => o != null);
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

#if NETFRAMEWORK

	/// <summary>
	/// You can use this class instead of resolver.
	/// </summary>
	public class EntityFrameworkConfiguration : DbConfiguration
	{
		public EntityFrameworkConfiguration()
		{
			// "Microsoft.Data.SqlClient"
			// https://learn.microsoft.com/en-us/ef/ef6/what-is-new/microsoft-ef6-sqlserver
			var msSqlInvariantName = MicrosoftSqlProviderServices.ProviderInvariantName;
			SetProviderFactory(msSqlInvariantName, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
			SetProviderServices(msSqlInvariantName, MicrosoftSqlProviderServices.Instance);
			SetExecutionStrategy(msSqlInvariantName, () => new MicrosoftSqlAzureExecutionStrategy());

			// Override hardcoded "System.Data.SqlClient"
			// https://learn.microsoft.com/en-us/ef/ef6/what-is-new/microsoft-ef6-sqlserver
			//var dataSqlInstance = System.Data.SqlClient.SqlClientFactory.Instance;
			var dataSqlInvariantName = "System.Data.SqlClient";
			SetProviderFactory(dataSqlInvariantName, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
			SetProviderServices(dataSqlInvariantName, MicrosoftSqlProviderServices.Instance);
			SetExecutionStrategy(dataSqlInvariantName, () => new MicrosoftSqlAzureExecutionStrategy());

			// "System.Data.SQLite.EF6"
			var liteInstance = System.Data.SQLite.EF6.SQLiteProviderFactory.Instance;
			var liteInvarialName = liteInstance.GetType().Name;
			SetProviderFactory(liteInvarialName, System.Data.SQLite.EF6.SQLiteProviderFactory.Instance);
			var liteServiceType = typeof(System.Data.Entity.Core.Common.DbProviderServices);
			SetProviderServices(liteInvarialName, (System.Data.Entity.Core.Common.DbProviderServices)liteInstance.GetService(liteServiceType));
		}
	}

#endif


}
