using System;
using System.Data;
using System.Data.SqlClient;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Execute queries or stored procedures on database.
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
		public int ExecuteNonQuery(string connectionString, string cmdText, string cmdType)
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
		public string ExecuteDataTable(string connectionString, string cmdText, string cmdType)
		{
			var cmd = new SqlCommand(cmdText);
			cmd.CommandType = (CommandType)Enum.Parse(typeof(CommandType), cmdType);
			var helper = new SqlHelper();
			var table = helper.ExecuteDataTable(connectionString, cmd);
			return CsvHelper.Write(table);
		}

	}
}
