using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	internal class SqlHelper
	{

		#region Execute Methods

		public int ExecuteNonQuery(string connectionString, SqlCommand cmd, string comment = null, int? timeout = null)
		{
			//var sql = ToSqlCommandString(cmd);
			var cb = new SqlConnectionStringBuilder(connectionString);
			if (timeout.HasValue)
			{
				cmd.CommandTimeout = timeout.Value;
				cb.ConnectTimeout = timeout.Value;
			}
			var conn = new SqlConnection(cb.ConnectionString);
			cmd.Connection = conn;
			conn.Open();
			//SetSessionUserCommentContext(conn, comment);
			int rv = cmd.ExecuteNonQuery();
			cmd.Dispose();
			// Dispose calls conn.Close() internally.
			conn.Dispose();
			return rv;
		}

		//[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		//public int ExecuteNonQuery(string connectionString, string cmdText, string comment = null, int? timeout = null)
		//{
		//	var cmd = new SqlCommand(cmdText);
		//	cmd.CommandType = CommandType.Text;
		//	return ExecuteNonQuery(connectionString, cmd, comment, timeout);
		//}

		public object ExecuteScalar(string connectionString, SqlCommand cmd, string comment = null)
		{
			//var sql = ToSqlCommandString(cmd);
			var conn = new SqlConnection(connectionString);
			cmd.Connection = conn;
			conn.Open();
			//SetSessionUserCommentContext(conn, comment);
			// Returns first column of the first row.
			var returnValue = cmd.ExecuteScalar();
			cmd.Dispose();
			// Dispose calls conn.Close() internally.
			conn.Dispose();
			return returnValue;
		}

		public IDataReader ExecuteReader(string connectionString, SqlCommand cmd, string comment = null)
		{
			//var sql = ToSqlCommandString(cmd);
			var conn = new SqlConnection(connectionString);
			cmd.Connection = conn;
			conn.Open();
			//SetSessionUserCommentContext(conn, comment);
			return cmd.ExecuteReader();
		}

		public T ExecuteDataSet<T>(string connectionString, SqlCommand cmd, string comment = null) where T : DataSet
		{
			//var sql = ToSqlCommandString(cmd);
			var conn = new SqlConnection(connectionString);
			cmd.Connection = conn;
			conn.Open();
			//SetSessionUserCommentContext(conn, comment);
			var adapter = new SqlDataAdapter(cmd);
			var ds = Activator.CreateInstance<T>();
			int rowsAffected = ds.GetType() == typeof(DataSet)
				? adapter.Fill(ds)
				: adapter.Fill(ds, ds.Tables[0].TableName);
			adapter.Dispose();
			cmd.Dispose();
			// Dispose calls conn.Close() internally.
			conn.Dispose();
			return ds;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		public DataSet ExecuteDataset(string connectionString, CommandType commandType, string cmdText, string comment = null, params SqlParameter[] commandParameters)
		{
			var cmd = new SqlCommand(cmdText);
			cmd.CommandType = commandType;
			if (commandParameters != null && commandParameters.Length > 0)
			{
				cmd.Parameters.AddRange(commandParameters);
			}
			return ExecuteDataSet<DataSet>(connectionString, cmd, comment);
		}

		public DataSet ExecuteDataSet(string connectionString, SqlCommand cmd, string comment = null)
		{
			return ExecuteDataSet<DataSet>(connectionString, cmd, comment);
		}

		public DataTable ExecuteDataTable(string connectionString, SqlCommand cmd, string comment = null)
		{
			var ds = ExecuteDataSet(connectionString, cmd, comment);
			if (ds != null && ds.Tables.Count > 0) return ds.Tables[0];
			return null;
		}

		public DataRow ExecuteDataRow(string connectionString, SqlCommand cmd, string comment = null)
		{
			var table = ExecuteDataTable(connectionString, cmd, comment);
			if (table != null && table.Rows.Count > 0) return table.Rows[0];
			return null;
		}

		public List<T> ExecuteData<T>(string connectionString, SqlCommand cmd, string comment = null)
		{
			var list = new List<T>();
			var props = typeof(T).GetProperties().ToDictionary(x => x.Name, x => x);
			var reader = ExecuteReader(connectionString, cmd, comment);
			while (reader.Read())
			{
				var item = Activator.CreateInstance<T>();
				for (int i = 0; i < reader.FieldCount; i++)
				{
					var name = reader.GetName(i);
					var value = reader.GetValue(i);
					var property = props[name];
					if (property != null)
						property.SetValue(item, reader.IsDBNull(i) ? null : value, null);
				}
				list.Add(item);
			}
			return null;
		}

		#endregion

	}
}
