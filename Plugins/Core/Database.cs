using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Data;
using JocysCom.ClassLibrary.Files;
using LiteDB;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Allows AI to execute queries or stored procedures on a database. For example, it can retrieve a database schema and construct complex results. Use database permissions to restrict AI's access.
	/// </summary>
	public partial class Database
	{
		/// <summary>Execute non query command on database. Return number of rows affected.</summary>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <param name="cmdType">SQL Command Type.
		/// "Text" = An SQL text command.
		/// "StoredProcedure" - The name of a stored procedure.
		/// "TableDirect" - The name of a table.
		/// </param>
		/// <param name="connectionString">
		/// The database connection string.
		/// If the connection string is not provided, the default SQLite database is used.
		/// The SQL syntax and execution behavior will be determined based on the database type specified in the connection string (e.g., SQLite, MS-SQL, Oracle).
		/// </param>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<int> ExecuteNonQuery(string cmdText, string cmdType, string connectionString = null)
		{
			try
			{
				var cmd = string.IsNullOrEmpty(connectionString)
					? (DbCommand)new SqliteCommand(cmdText)
					: (DbCommand)new SqlCommand(cmdText);
				if (string.IsNullOrEmpty(connectionString))
					connectionString = GetSqliteConnectionSring();
				cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
				var helper = new ClassLibrary.Data.SqlHelper();
				var rowsAffected = helper.ExecuteNonQuery(connectionString, cmd);
				return new OperationResult<int>(rowsAffected);
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}

		}

		/// <summary>SQL query command on database. Returns resutls as CSV.</summary>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <param name="cmdType">SQL Command Type.
		/// "Text" = An SQL text command.
		/// "StoredProcedure" - The name of a stored procedure.
		/// "TableDirect" - The name of a table.
		/// </param>
		/// <param name="connectionString">
		/// The database connection string.
		/// If the connection string is not provided, the default SQLite database is used.
		/// The SQL syntax and execution behavior will be determined based on the database type specified in the connection string (e.g., SQLite, MS-SQL, Oracle).
		/// </param>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<string> ExecuteDataTable(string cmdText, string cmdType, string connectionString = null)
		{
			try
			{
				var cmd = string.IsNullOrEmpty(connectionString)
					? (DbCommand)new SqliteCommand(cmdText)
					: (DbCommand)new SqlCommand(cmdText);
				if (string.IsNullOrEmpty(connectionString))
					connectionString = GetSqliteConnectionSring();
				cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
				var helper = new ClassLibrary.Data.SqlHelper();
				var table = helper.ExecuteDataTable(connectionString, cmd);
				var csvContents = CsvHelper.Write(table);
				return new OperationResult<string>(csvContents);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Get description text of database schema, table or column.
		/// Specify only the levels relevant to the target object for which the description is being set.
		/// When specifying a column, for instance, both the table and schema to which it belongs must also be indicated.
		/// </summary>
		/// <param name="connectionString">Databse connection string.</param>
		/// <param name="schema">Schema name.</param>
		/// <param name="table">Table name.</param>
		/// <param name="column">Column name.</param>
		/// <returns>Item description.</returns>
		[RiskLevel(RiskLevel.High)]
		public string GetDescription(string connectionString, string schema = null, string table = null, string column = null)
		{
			return ClassLibrary.Data.SqlHelper.GetProperty(connectionString, "MS_Description", schema, table, column);
		}

		/// <summary>
		/// Set description of database schema, table or column.
		/// Specify only the levels relevant to the target object for which the description is being set.
		/// When specifying a column, for instance, both the table and schema to which it belongs must also be indicated.
		/// </summary>
		/// <param name="connectionString">Database connection string.</param>
		/// <param name="schema">Schema name.</param>
		/// <param name="table">Table name.</param>
		/// <param name="column">Column name.</param>
		/// <param name="description">Description text.</param>
		/// <returns>0 (success) or 1 (failure).</returns>
		[RiskLevel(RiskLevel.High)]
		public int SetDescription(string connectionString, string description, string schema = null, string table = null, string column = null)
		{
			return ClassLibrary.Data.SqlHelper.SetProperty(connectionString, "MS_Description", description, schema, table, column);
		}

		#region SQLite Portable Database

		/// <summary>
		/// Get path to databases folder.
		/// </summary>
		public Func<string> GetDatabasesFolderPath { get; set; }
		private string databaseFileExtension = ".sqlite";
		private string _DatabaseName = "Default";

		private string GetSqliteConnectionSring()
		{
			var path = GetDatabasesFolderPath();
			var fullFileName = System.IO.Path.Combine(path, _DatabaseName + databaseFileExtension);
			var builder = new SqliteConnectionStringBuilder();
			builder.DataSource = fullFileName;
			return builder.ToString();
		}

		/// <summary>
		/// Get SQLite database names filtered by an optional regular expression pattern.
		/// </summary>
		/// <param name="pattern">The regex pattern to filter database names. If null, all names are returned.</param>
		/// <returns>A List of filtered database names.</returns>
		private List<string> GetDatabaseNames(string pattern = null)
		{
			var path = GetDatabasesFolderPath();
			if (!Directory.Exists(path))
				return new List<string>();
			var names = System.IO.Directory
				.GetFiles(path, $"*{databaseFileExtension}")
				.Select(x => System.IO.Path.GetFileNameWithoutExtension(x));
			if (pattern != null)
			{
				var regex = new Regex(pattern, RegexOptions.IgnoreCase);
				names = names.Where(x => regex.IsMatch(x));
			}
			return names.ToList();
		}

		/// <summary>
		/// Creates a new SQLite database.
		/// </summary>
		/// <param name="databaseName">Name of the database</param>
		/// <returns>0 operation successfull, -3 database already exists.</returns>
		private int CreateDatabase(string databaseName)
		{
			var names = GetDatabaseNames(databaseName);
			// List already exists.
			if (names.Any())
				return -3;
			var path = GetDatabasesFolderPath();
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			var fullName = System.IO.Path.Combine(path, $"{databaseName}{databaseFileExtension}");
			if (!File.Exists(fullName))
				System.IO.File.WriteAllText(fullName, "");
			return 0;
		}

		/// <summary>
		/// Deletes an existing SQLite database.
		/// </summary>
		/// <param name="databaseName">Name of the database</param>
		/// <returns>0 operation successfull, -1 database not found, -2 database is readonly.</returns>
		private int DeleteDatabase(string databaseName)
		{
			var names = GetDatabaseNames(databaseName);
			if (!names.Any())
				return -1;
			var path = GetDatabasesFolderPath();
			var fullFileName = System.IO.Path.Combine(path, databaseName + databaseFileExtension);
			var isReadOnly = System.IO.File.GetAttributes(path).HasFlag(System.IO.FileAttributes.ReadOnly);
			if (isReadOnly)
				return -2;
			System.IO.File.Delete(fullFileName);
			return 0;
		}

		/// <summary>
		/// Searches for files and inserts them into a database table with a unique Id column.
		/// </summary>
		/// <param name="connectionString">
		/// The database connection string.
		/// If the connection string is not provided, the default SQLite database is used.
		/// The SQL syntax and execution behavior will be determined based on the database type specified in the connection string (e.g., SQLite, MS-SQL, Oracle).
		/// </param>
		/// <param name="tableName">Name of the table to insert files into.</param>
		/// <param name="searchPath">Directory path to search for files.</param>
		/// <param name="searchPattern">Search pattern for files (e.g., "*.txt").</param>
		/// <param name="allDirectories">Whether to search all subdirectories.</param>
		/// <param name="includePatterns">Patterns to include.</param>
		/// <param name="excludePatterns">Patterns to exclude.</param>
		/// <param name="useGitIgnore">Whether to respect .gitignore files.</param>
		/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
		/// <returns>Number of files inserted.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public async Task<OperationResult<int>> SearchAndSaveFilesToTable(
			string connectionString,
			string tableName,
			string searchPath,
			string searchPattern,
			bool allDirectories = false,
			string includePatterns = null,
			string excludePatterns = null,
			bool useGitIgnore = false,
			CancellationToken cancellationToken = default
		)
		{
			var fileHelper = new FileHelper();
			List<FileInfo> files;
			try
			{
				files = fileHelper.FindFiles(
					searchPath,
					searchPattern,
					allDirectories,
					includePatterns,
					excludePatterns,
					useGitIgnore
				);
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}

			try
			{
				var helper = new ClassLibrary.Data.SqlHelper();
				if (string.IsNullOrEmpty(connectionString))
				{
					connectionString = GetSqliteConnectionSring();
					CreateDatabase(_DatabaseName);
				}

				var fullName = nameof(FileInfo.FullName);
				var name = nameof(FileInfo.Name);
				var length = nameof(FileInfo.Length);
				var creationTimeUtc = nameof(FileInfo.CreationTimeUtc);
				var lastWriteTimeUtc = nameof(FileInfo.LastWriteTimeUtc);
				var status = "Status";
				var id = "Id";

				var isPortable = SqlHelper.IsPortable(connectionString);

				var connection = isPortable
					? (DbConnection)new SqliteConnection(connectionString)
					: (DbConnection)new SqlConnection(connectionString);
				await connection.OpenAsync();

				var containsTable = SqlHelper.ContainsTable(tableName, connection);
				var sqliteSchema =
					$"[{id}] INTEGER PRIMARY KEY AUTOINCREMENT,\r\n" +
					$"[{fullName}] TEXT,\r\n" +
					$"[{name}] TEXT,\r\n" +
					$"[{length}] INTEGER,\r\n" +
					$"[{creationTimeUtc}] TEXT,\r\n" +
					$"[{lastWriteTimeUtc}] TEXT,\r\n" +
					$"[{status}] TEXT\r\n";
				var sqlSchema =
					$"[{id}] INT IDENTITY(1,1) PRIMARY KEY,\r\n" +
					$"[{fullName}] NVARCHAR(MAX),\r\n" +
					$"[{name}] NVARCHAR(MAX),\r\n" +
					$"[{length}] BIGINT,\r\n" +
					$"[{creationTimeUtc}] DATETIME,\r\n" +
					$"[{lastWriteTimeUtc}] DATETIME,\r\n" +
					$"[{status}] NVARCHAR(MAX)\r\n";
				var schema = isPortable ? sqliteSchema : sqlSchema;
				if (!containsTable)
				{
					// Table does not exist; create it with a unique Id column.
					var createTableQuery = $"CREATE TABLE {tableName} (\r\n{schema});";
					var createCmd = isPortable
						? (DbCommand)new SqliteCommand(createTableQuery)
						: (DbCommand)new SqlCommand(createTableQuery);
					createCmd.Connection = connection;
					createCmd.ExecuteNonQuery();
				}

				// Prepare the insert command.
				var insertQuery = $@"
            INSERT INTO {tableName} ({fullName}, {name}, {length}, {creationTimeUtc}, {lastWriteTimeUtc}, {status})
            VALUES (@{fullName}, @{name}, @{length}, @{creationTimeUtc}, @{lastWriteTimeUtc}, @{status});";

				var insertCmd = isPortable
					? (DbCommand)new SqliteCommand(insertQuery)
					: (DbCommand)new SqlCommand(insertQuery);
				insertCmd.Connection = connection;

				// Add parameters.
				SqlHelper.AddParameter(insertCmd, $"@{fullName}", "");
				SqlHelper.AddParameter(insertCmd, $"@{name}", "");
				SqlHelper.AddParameter(insertCmd, $"@{length}", 0);
				SqlHelper.AddParameter(insertCmd, $"@{creationTimeUtc}", "");
				SqlHelper.AddParameter(insertCmd, $"@{lastWriteTimeUtc}", "");
				SqlHelper.AddParameter(insertCmd, $"@{status}", "");

				int insertCount = 0;

				// For each file, set parameter values and execute insert.
				foreach (var file in files)
				{
					if (cancellationToken.IsCancellationRequested)
						return new OperationResult<int>(new Exception("User cancelled the action."));

					insertCmd.Parameters[$"@{fullName}"].Value = file.FullName;
					insertCmd.Parameters[$"@{name}"].Value = file.Name;
					insertCmd.Parameters[$"@{length}"].Value = file.Length;
					insertCmd.Parameters[$"@{creationTimeUtc}"].Value = file.CreationTimeUtc.ToString("o"); // ISO 8601 format
					insertCmd.Parameters[$"@{lastWriteTimeUtc}"].Value = file.LastWriteTimeUtc.ToString("o");
					insertCmd.Parameters[$"@{status}"].Value = "";

					insertCount += insertCmd.ExecuteNonQuery();
				}
				var databaseSyntax = isPortable ? " on SQLite database" : "";

				// Prepare table schema description for the status message.
				var statusText = $@"Table '{tableName}' created{databaseSyntax} with schema:\r\n {schema}";
				// Return the number of files inserted along with the table schema description.
				return new OperationResult<int>(insertCount, 0, statusText);
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}
		}
		#endregion

	}
}
