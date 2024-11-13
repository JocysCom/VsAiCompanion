using JocysCom.ClassLibrary.Files;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

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
		public static int ExecuteNonQuery(string connectionString, string cmdText, string cmdType)
		{
			var cmd = new SqlCommand(cmdText);
			cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
			var helper = new SqlHelper();
			return helper.ExecuteNonQuery(connectionString, cmd);
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
		public static string ExecuteDataTable(string connectionString, string cmdText, string cmdType)
		{
			var cmd = new SqlCommand(cmdText);
			cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
			var helper = new SqlHelper();
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
		public static string GetDescription(string connectionString, string schema = null, string table = null, string column = null)
		{
			return SqlHelper.GetProperty(connectionString, "MS_Description", schema, table, column);
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
		public static int SetDescription(string connectionString, string description, string schema = null, string table = null, string column = null)
		{
			return SqlHelper.SetProperty(connectionString, "MS_Description", description, schema, table, column);
		}

	}
}
