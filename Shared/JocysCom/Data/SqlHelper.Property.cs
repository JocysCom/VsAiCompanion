using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace JocysCom.ClassLibrary.Data
{
	/// <summary>
	/// SQL Data Helper.
	/// </summary>
	public partial class SqlHelper
	{

		public static string GetProperty(string connectionString, string name, string schema = null, string table = null, string column = null)
		{
			var level0 = string.IsNullOrEmpty(schema) ? null : "SCHEMA";
			var level1 = string.IsNullOrEmpty(table) ? null : "TABLE";
			var level2 = string.IsNullOrEmpty(column) ? null : "COLUMN";
			var row = fn_listextendedproperty(
				connectionString,
				name,
				level0, schema,
				level1, table,
				level2, column);
			var value = row == null ? null : row["value"] as string;
			return value;
		}

		/// <returns>0 (success) or 1 (failure).</returns>
		public static int SetProperty(string connectionString, string name, string value, string schema = null, string table = null, string column = null)
		{
			var level0 = string.IsNullOrEmpty(schema) ? null : "SCHEMA";
			var level1 = string.IsNullOrEmpty(table) ? null : "TABLE";
			var level2 = string.IsNullOrEmpty(column) ? null : "COLUMN";
			var row = fn_listextendedproperty(
				connectionString,
				name,
				level0, schema,
				level1, table,
				level2, column);
			if (value == null)
			{
				if (row == null)
					return 0;
				return sp_extendedproperty(
					connectionString,
					-1,
					name, null,
					level0, schema,
					level1, table,
					level2, column
				);
			}
			return sp_extendedproperty(
					connectionString,
					row == null ? 1 : 0,
					name, value,
					level0, schema,
					level1, table,
					level2, column
				);
		}

		private static DataRow fn_listextendedproperty(
			string connectionString,
			string name,
			string level0type = null,
			string level0name = null,
			string level1type = null,
			string level1name = null,
			string level2type = null,
			string level2name = null
	)
		{
			var cmd = new SqlCommand("SELECT * FROM sys.fn_listextendedproperty(@property_name, @level0_object_type, @level0_object_name, @level1_object_type, @level1_object_name, @level2_object_type, @level2_object_name)");
			cmd.CommandType = CommandType.Text;
			var p = cmd.Parameters;
			p.AddWithValue("@property_name", name);
			p.Add("@level0_object_type", SqlDbType.VarChar, 128).Value = level0type ?? (object)DBNull.Value;
			p.Add("@level0_object_name", SqlDbType.VarChar, 128).Value = level0name ?? (object)DBNull.Value;
			p.Add("@level1_object_type", SqlDbType.VarChar, 128).Value = level1type ?? (object)DBNull.Value;
			p.Add("@level1_object_name", SqlDbType.VarChar, 128).Value = level1name ?? (object)DBNull.Value;
			p.Add("@level2_object_type", SqlDbType.VarChar, 128).Value = level2type ?? (object)DBNull.Value;
			p.Add("@level2_object_name", SqlDbType.VarChar, 128).Value = level2name ?? (object)DBNull.Value;
			var helper = new SqlHelper();
			var data = helper.ExecuteDataRow(connectionString, cmd);
			cmd.Dispose();
			return data;
		}

		private static int sp_extendedproperty(
			string connectionString,
			int action,
			string name,
			object value = null,
			string level0type = null,
			string level0name = null,
			string level1type = null,
			string level1name = null,
			string level2type = null,
			string level2name = null
		)
		{
			// -1 - Delete, 0 - Update, 1 - Add.
			var sp = "sys.sp_updateextendedproperty";
			if (action == -1)
				sp = "sys.sp_dropextendedproperty";
			if (action == 1)
				sp = "sys.sp_addextendedproperty";
			var cmd = new SqlCommand(sp);
			cmd.CommandType = CommandType.StoredProcedure;
			var p = cmd.Parameters;
			p.AddWithValue("@name", name);
			if (value != null)
				p.AddWithValue("@value", value);
			p.Add("@level0type", SqlDbType.VarChar, 128).Value = level0type ?? (object)DBNull.Value;
			p.Add("@level0name", SqlDbType.VarChar, 128).Value = level0name ?? (object)DBNull.Value;
			p.Add("@level1type", SqlDbType.VarChar, 128).Value = level1type ?? (object)DBNull.Value;
			p.Add("@level1name", SqlDbType.VarChar, 128).Value = level1name ?? (object)DBNull.Value;
			p.Add("@level2type", SqlDbType.VarChar, 128).Value = level2type ?? (object)DBNull.Value;
			p.Add("@level2name", SqlDbType.VarChar, 128).Value = level2name ?? (object)DBNull.Value;
			var rv = new SqlParameter("@returnValue", SqlDbType.Int);
			rv.Direction = ParameterDirection.ReturnValue;
			p.Add(rv);
			var helper = new SqlHelper();
			var data = helper.ExecuteNonQuery(connectionString, cmd);
			cmd.Dispose();
			var returnValue = (int)rv.Value;
			return returnValue;
		}

	}
}
