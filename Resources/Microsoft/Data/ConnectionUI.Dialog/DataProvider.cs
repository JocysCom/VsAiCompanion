using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.OracleClient;

namespace Microsoft.Data.ConnectionUI
{
	public class DataProvider
	{
		private static DataProvider _sqlDataProvider;
		private static DataProvider _oracleDataProvider;
		private static DataProvider _oleDBDataProvider;
		private static DataProvider _odbcDataProvider;
		private string _name;
		private string _displayName;
		private string _shortDisplayName;
		private string _description;
		private Type _targetConnectionType;
		private IDictionary<string, string> _dataSourceDescriptions;
		private IDictionary<string, Type> _connectionUIControlTypes;
		private IDictionary<string, Type> _connectionPropertiesTypes;

		public DataProvider(string name, string displayName, string shortDisplayName)
		  : this(name, displayName, shortDisplayName, (string)null, (Type)null)
		{
		}

		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description)
		  : this(name, displayName, shortDisplayName, description, (Type)null)
		{
		}

		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType)
		{
			_name = name != null ? name : throw new ArgumentNullException(nameof(name));
			_displayName = displayName;
			_shortDisplayName = shortDisplayName;
			_description = description;
			_targetConnectionType = targetConnectionType;
		}

		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType,
		  Type connectionPropertiesType)
		  : this(name, displayName, shortDisplayName, description, targetConnectionType)
		{
			if (connectionPropertiesType == (Type)null)
				throw new ArgumentNullException(nameof(connectionPropertiesType));
			_connectionPropertiesTypes = (IDictionary<string, Type>)new Dictionary<string, Type>();
			_connectionPropertiesTypes.Add(string.Empty, connectionPropertiesType);
		}

		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType,
		  Type connectionUIControlType,
		  Type connectionPropertiesType)
		  : this(name, displayName, shortDisplayName, description, targetConnectionType, connectionPropertiesType)
		{
			if (connectionUIControlType == (Type)null)
				throw new ArgumentNullException(nameof(connectionUIControlType));
			_connectionUIControlTypes = (IDictionary<string, Type>)new Dictionary<string, Type>();
			_connectionUIControlTypes.Add(string.Empty, connectionUIControlType);
		}

		[CLSCompliant(false)]
		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType,
		  IDictionary<string, Type> connectionUIControlTypes,
		  Type connectionPropertiesType)
		  : this(name, displayName, shortDisplayName, description, targetConnectionType, connectionPropertiesType)
		{
			_connectionUIControlTypes = connectionUIControlTypes;
		}

		[CLSCompliant(false)]
		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType,
		  IDictionary<string, string> dataSourceDescriptions,
		  IDictionary<string, Type> connectionUIControlTypes,
		  Type connectionPropertiesType)
		  : this(name, displayName, shortDisplayName, description, targetConnectionType, connectionUIControlTypes, connectionPropertiesType)
		{
			_dataSourceDescriptions = dataSourceDescriptions;
		}

		[CLSCompliant(false)]
		public DataProvider(
		  string name,
		  string displayName,
		  string shortDisplayName,
		  string description,
		  Type targetConnectionType,
		  IDictionary<string, string> dataSourceDescriptions,
		  IDictionary<string, Type> connectionUIControlTypes,
		  IDictionary<string, Type> connectionPropertiesTypes)
		  : this(name, displayName, shortDisplayName, description, targetConnectionType)
		{
			_dataSourceDescriptions = dataSourceDescriptions;
			_connectionUIControlTypes = connectionUIControlTypes;
			_connectionPropertiesTypes = connectionPropertiesTypes;
		}

		public static DataProvider SqlDataProvider
		{
			get
			{
				if (DataProvider._sqlDataProvider == null)
					DataProvider._sqlDataProvider = new DataProvider("System.Data.SqlClient", SR.GetString("DataProvider_Sql"), SR.GetString("DataProvider_Sql_Short"), SR.GetString("DataProvider_Sql_Description"), typeof(SqlConnection), (IDictionary<string, string>)new Dictionary<string, string>()
		  {
			{
			  DataSource.SqlDataSource.Name,
			  SR.GetString("DataProvider_Sql_DataSource_Description")
			},
			{
			  DataSource.SqlFileDataSource.Name,
			  SR.GetString("DataProvider_Sql_FileDataSource_Description")
			}
		  }, (IDictionary<string, Type>)new Dictionary<string, Type>()
		  {
			{
			  DataSource.SqlDataSource.Name,
			  typeof (SqlConnectionUIControl)
			},
			{
			  DataSource.SqlFileDataSource.Name,
			  typeof (SqlFileConnectionUIControl)
			},
			{
			  string.Empty,
			  typeof (SqlConnectionUIControl)
			}
		  }, (IDictionary<string, Type>)new Dictionary<string, Type>()
		  {
			{
			  DataSource.SqlFileDataSource.Name,
			  typeof (SqlFileConnectionProperties)
			},
			{
			  string.Empty,
			  typeof (SqlConnectionProperties)
			}
		  });
				return DataProvider._sqlDataProvider;
			}
		}

		public static DataProvider OracleDataProvider
		{
			get
			{
				if (DataProvider._oracleDataProvider == null)
#pragma warning disable CS0618 // Type or member is obsolete
					DataProvider._oracleDataProvider = new DataProvider(
						"System.Data.OracleClient",
						SR.GetString("DataProvider_Oracle"),
						SR.GetString("DataProvider_Oracle_Short"),
						SR.GetString("DataProvider_Oracle_Description"),
						typeof(OracleConnection),
						(IDictionary<string, string>)new Dictionary<string, string>()
					{
			{
			  DataSource.OracleDataSource.Name,
			  SR.GetString("DataProvider_Oracle_DataSource_Description")
			}
					}, (IDictionary<string, Type>)new Dictionary<string, Type>()
					{
			{
			  string.Empty,
			  typeof (OracleConnectionUIControl)
			}
					}, typeof(OracleConnectionProperties));
#pragma warning restore CS0618 // Type or member is obsolete
				return DataProvider._oracleDataProvider;
			}
		}

		public static DataProvider OleDBDataProvider
		{
			get
			{
				if (DataProvider._oleDBDataProvider == null)
					DataProvider._oleDBDataProvider = new DataProvider("System.Data.OleDb", SR.GetString("DataProvider_OleDB"), SR.GetString("DataProvider_OleDB_Short"), SR.GetString("DataProvider_OleDB_Description"), typeof(OleDbConnection), (IDictionary<string, string>)new Dictionary<string, string>()
		  {
			{
			  DataSource.SqlDataSource.Name,
			  SR.GetString("DataProvider_OleDB_SqlDataSource_Description")
			},
			{
			  DataSource.OracleDataSource.Name,
			  SR.GetString("DataProvider_OleDB_OracleDataSource_Description")
			},
			{
			  DataSource.AccessDataSource.Name,
			  SR.GetString("DataProvider_OleDB_AccessDataSource_Description")
			}
		  }, (IDictionary<string, Type>)new Dictionary<string, Type>()
		  {
			{
			  DataSource.SqlDataSource.Name,
			  typeof (SqlConnectionUIControl)
			},
			{
			  DataSource.OracleDataSource.Name,
			  typeof (OracleConnectionUIControl)
			},
			{
			  DataSource.AccessDataSource.Name,
			  typeof (AccessConnectionUIControl)
			},
			{
			  string.Empty,
			  typeof (OleDBConnectionUIControl)
			}
		  }, (IDictionary<string, Type>)new Dictionary<string, Type>()
		  {
			{
			  DataSource.SqlDataSource.Name,
			  typeof (OleDBSqlConnectionProperties)
			},
			{
			  DataSource.OracleDataSource.Name,
			  typeof (OleDBOracleConnectionProperties)
			},
			{
			  DataSource.AccessDataSource.Name,
			  typeof (OleDBAccessConnectionProperties)
			},
			{
			  string.Empty,
			  typeof (OleDBConnectionProperties)
			}
		  });
				return DataProvider._oleDBDataProvider;
			}
		}

		public static DataProvider OdbcDataProvider
		{
			get
			{
				if (DataProvider._odbcDataProvider == null)
					DataProvider._odbcDataProvider = new DataProvider("System.Data.Odbc", SR.GetString("DataProvider_Odbc"), SR.GetString("DataProvider_Odbc_Short"), SR.GetString("DataProvider_Odbc_Description"), typeof(OdbcConnection), (IDictionary<string, string>)new Dictionary<string, string>()
		  {
			{
			  DataSource.OdbcDataSource.Name,
			  SR.GetString("DataProvider_Odbc_DataSource_Description")
			}
		  }, (IDictionary<string, Type>)new Dictionary<string, Type>()
		  {
			{
			  string.Empty,
			  typeof (OdbcConnectionUIControl)
			}
		  }, typeof(OdbcConnectionProperties));
				return DataProvider._odbcDataProvider;
			}
		}

		public string Name => _name;

		public string DisplayName => _displayName == null ? _name : _displayName;

		public string ShortDisplayName => _shortDisplayName;

		public string Description => GetDescription((DataSource)null);

		public Type TargetConnectionType => _targetConnectionType;

		public virtual string GetDescription(DataSource dataSource)
		{
			return _dataSourceDescriptions != null && dataSource != null && _dataSourceDescriptions.ContainsKey(dataSource.Name) ? _dataSourceDescriptions[dataSource.Name] : _description;
		}

		public IDataConnectionUIControl CreateConnectionUIControl()
		{
			return CreateConnectionUIControl((DataSource)null);
		}

		public virtual IDataConnectionUIControl CreateConnectionUIControl(DataSource dataSource)
		{
			string key;
			return _connectionUIControlTypes != null && dataSource != null && _connectionUIControlTypes.ContainsKey(key = dataSource.Name) || _connectionUIControlTypes.ContainsKey(key = string.Empty) ? Activator.CreateInstance(_connectionUIControlTypes[key]) as IDataConnectionUIControl : (IDataConnectionUIControl)null;
		}

		public IDataConnectionProperties CreateConnectionProperties()
		{
			return CreateConnectionProperties((DataSource)null);
		}

		public virtual IDataConnectionProperties CreateConnectionProperties(DataSource dataSource)
		{
			string key;
			return _connectionPropertiesTypes != null && (dataSource != null && _connectionPropertiesTypes.ContainsKey(key = dataSource.Name) || _connectionPropertiesTypes.ContainsKey(key = string.Empty)) ? Activator.CreateInstance(_connectionPropertiesTypes[key]) as IDataConnectionProperties : (IDataConnectionProperties)null;
		}
	}
}
