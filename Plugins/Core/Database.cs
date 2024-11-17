using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Files;
using LiteDB;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Allows AI to execute queries or stored procedures on a database. For example, it can retrieve a database schema and construct complex results. Use database permissions to restrict AI's access.
	/// </summary>
	public partial class Database
	{
		/// <summary>
		/// Execute non query command on database. Return number of rows affected.
		/// </summary>
		/// <param name="connectionString">Databse connection string.</param>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <param name="cmdType">SQL Command Type.
		/// "Text" = An SQL text command.
		/// "StoredProcedure" - The name of a stored procedure.
		/// "TableDirect" - The name of a table.
		/// </param>
		[RiskLevel(RiskLevel.High)]
		public async Task<OperationResult<int>> ExecuteNonQuery(string cmdText, string cmdType, string connectionString = null)
		{
			if (string.IsNullOrEmpty(connectionString))
			{
				return await ExecuteNonQueryByDatabaseName(_DatabaseName, cmdText);
			}
			else
			{
				var cmd = new SqlCommand(cmdText);
				cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
				var helper = new ClassLibrary.Data.SqlHelper();
				var rowsAffected = helper.ExecuteNonQuery(connectionString, cmd);
				return new OperationResult<int>(rowsAffected);
			}
		}

		/// <summary>
		/// SQL query command on database. Returns resutls as CSV.
		/// </summary>
		/// <param name="connectionString">Databse connection string.</param>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <param name="cmdType">SQL Command Type.
		/// "Text" = An SQL text command.
		/// "StoredProcedure" - The name of a stored procedure.
		/// "TableDirect" - The name of a table.
		/// </param>
		[RiskLevel(RiskLevel.High)]
		public string ExecuteDataTable(string connectionString, string cmdText, string cmdType)
		{
			var cmd = new SqlCommand(cmdText);
			cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
			var helper = new ClassLibrary.Data.SqlHelper();
			var table = helper.ExecuteDataTable(connectionString, cmd);
			return CsvHelper.Write(table);
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

		/// <summary>
		/// Get SQLite database names filtered by an optional regular expression pattern.
		/// </summary>
		/// <param name="pattern">The regex pattern to filter database names. If null, all names are returned.</param>
		/// <returns>A List of filtered database names.</returns>
		private List<string> GetDatabaseNames(string pattern = null)
		{
			var path = GetDatabasesFolderPath();
			var names = System.IO.Directory
				.GetFiles($"*{databaseFileExtension}")
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
		/// Execute non query command on SQLite database. Return number of rows affected.
		/// </summary>
		/// <param name="databaseName">Database name.</param>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <returns>Number of rows affected.</returns>
		private async Task<OperationResult<int>> ExecuteNonQueryByDatabaseName(string databaseName, string cmdText)
		{
			var names = GetDatabaseNames(databaseName);
			if (!names.Any())
				return new OperationResult<int>(new Exception($"Database `{databaseName}` not found"));
			var path = GetDatabasesFolderPath();
			var fullFileName = System.IO.Path.Combine(path, databaseName + databaseFileExtension);

			try
			{
				using (var connection = new SqliteConnection(fullFileName))
				{
					connection.Open();
					using (var cmd = new SqliteCommand(cmdText, connection))
					{
						cmd.CommandType = CommandType.Text;
						int rowsAffected = await cmd.ExecuteNonQueryAsync();
						return new OperationResult<int>(rowsAffected);
					}
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}
		}


		/// <summary>
		/// Execute non query command on SQLite database. Returns resutls as CSV.
		/// </summary>
		/// <param name="databaseName">Database name.</param>
		/// <param name="cmdText">SQL Command Text.</param>
		/// <returns>Returns resutls as CSV.</returns>
		private async Task<OperationResult<string>> ExecuteDataTableByDatabaseName(string cmdText, string databaseName)
		{
			var names = GetDatabaseNames(databaseName);
			if (!names.Any())
				return new OperationResult<string>(new Exception($"Database `{databaseName}` not found"));
			var path = GetDatabasesFolderPath();
			var fullFileName = System.IO.Path.Combine(path, databaseName + databaseFileExtension);

			try
			{
				using (var connection = new SqliteConnection(fullFileName))
				{
					connection.Open();
					using (var cmd = new SqliteCommand(cmdText, connection))
					{
						cmd.CommandType = CommandType.Text;
						var table = new DataTable();
						using (var reader = await cmd.ExecuteReaderAsync())
							table.Load(reader);
						var csv = CsvHelper.Write(table);
						return new OperationResult<string>(csv);
					}
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Searches for files and inserts them into a table of a SQLite database.
		/// </summary>
		/// <param name="databaseName">Name of the database.</param>
		/// <param name="tableName">Name of the table to insert files into.</param>
		/// <param name="path">Directory path to search for files.</param>
		/// <param name="searchPattern">Search pattern for files (e.g., "*.txt").</param>
		/// <param name="allDirectories">Whether to search all subdirectories.</param>
		/// <param name="includePatterns">Patterns to include.</param>
		/// <param name="excludePatterns">Patterns to exclude.</param>
		/// <param name="useGitIgnore">Whether to respect .gitignore files.</param>
		/// <returns>Number of files inserted.</returns>
		private async Task<OperationResult<int>> InsertFilesToTableByDatabaseName(
			string databaseName,
			string tableName,
			string path,
			string searchPattern,
			bool allDirectories = false,
			string includePatterns = null,
			string excludePatterns = null,
			bool useGitIgnore = false
		)
		{
			// Validate the tableName to prevent SQL injection.
			if (string.IsNullOrWhiteSpace(tableName) || !Regex.IsMatch(tableName, @"^\w+$"))
				return new OperationResult<int>(new Exception($"Invalid table name `{tableName}`."));

			// Check if the database exists.
			var names = GetDatabaseNames(databaseName);
			if (!names.Any())
				return new OperationResult<int>(new Exception($"Database `{databaseName}` not found"));

			// Get the full path to the database file.
			var dbPath = GetDatabasesFolderPath();
			var fullFileName = System.IO.Path.Combine(dbPath, databaseName + databaseFileExtension);

			// Build the connection string.
			var connectionString = $"Data Source={fullFileName};";

			// Find the files.
			List<FileInfo> files;
			try
			{
				files = FileHelper.FindFiles(
					path,
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
				using (var connection = new SqliteConnection(connectionString))
				{
					await connection.OpenAsync();
					var fullName = nameof(FileInfo.FullName);
					var name = nameof(FileInfo.Name);
					var length = nameof(FileInfo.Length);
					var creationTimeUtc = nameof(FileInfo.CreationTimeUtc);
					var lastWriteTimeUtc = nameof(FileInfo.LastWriteTimeUtc);

					// Check if the table exists.
					string tableExistsQuery = @"SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
					using (var checkCmd = new SqliteCommand(tableExistsQuery, connection))
					{
						checkCmd.Parameters.AddWithValue("@tableName", tableName);
						var result = await checkCmd.ExecuteScalarAsync();
						if (result == null)
						{
							// Table does not exist; create it.
							string createTableQuery = $@"
                        CREATE TABLE {tableName} (
                            [{fullName}] TEXT,
                            [{name}] TEXT,
                            [{length}] INTEGER,
                            [{creationTimeUtc}] TEXT,
                            [{lastWriteTimeUtc}] TEXT
                        );";

							using (var createCmd = new SqliteCommand(createTableQuery, connection))
							{
								await createCmd.ExecuteNonQueryAsync();
							}
						}
					}

					// Begin a transaction for efficiency.
					using (var transaction = connection.BeginTransaction())
					{
						// Prepare the insert command.
						string insertQuery = $@"
                    INSERT INTO {tableName} ({fullName}, {name}, {length}, {creationTimeUtc}, {lastWriteTimeUtc})
                    VALUES (@FullPath, @{name}, @{length}, @{creationTimeUtc}, @{lastWriteTimeUtc});";

						using (var insertCmd = new SqliteCommand(insertQuery, connection, transaction))
						{
							// Add parameters.
							insertCmd.Parameters.Add($"@{fullName}", SqliteType.Text);
							insertCmd.Parameters.Add($"@{name}", SqliteType.Text);
							insertCmd.Parameters.Add($"@{length}", SqliteType.Integer);
							insertCmd.Parameters.Add($"@{creationTimeUtc}", SqliteType.Text);
							insertCmd.Parameters.Add($"@{lastWriteTimeUtc}", SqliteType.Text);
							int insertCount = 0;
							// For each file, set parameter values and execute insert.
							foreach (var file in files)
							{
								insertCmd.Parameters[$"@{fullName}"].Value = file.FullName;
								insertCmd.Parameters[$"@{name}"].Value = file.Name;
								insertCmd.Parameters[$"@{length}"].Value = file.Length;
								insertCmd.Parameters[$"@{creationTimeUtc}"].Value = file.CreationTimeUtc.ToString("o"); // ISO 8601 format
								insertCmd.Parameters[$"@{lastWriteTimeUtc}"].Value = file.LastWriteTimeUtc.ToString("o");
								await insertCmd.ExecuteNonQueryAsync();
								insertCount++;
							}
							// Commit the transaction.
							transaction.Commit();
							// Return the number of files inserted.
							return new OperationResult<int>(insertCount);
						}
					}
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}
		}

		#endregion

	}
}
