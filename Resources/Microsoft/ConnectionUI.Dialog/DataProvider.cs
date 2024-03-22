using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.OracleClient;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
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
      : this(name, displayName, shortDisplayName, (string) null, (Type) null)
    {
    }

    public DataProvider(
      string name,
      string displayName,
      string shortDisplayName,
      string description)
      : this(name, displayName, shortDisplayName, description, (Type) null)
    {
    }

    public DataProvider(
      string name,
      string displayName,
      string shortDisplayName,
      string description,
      Type targetConnectionType)
    {
      this._name = name != null ? name : throw new ArgumentNullException(nameof (name));
      this._displayName = displayName;
      this._shortDisplayName = shortDisplayName;
      this._description = description;
      this._targetConnectionType = targetConnectionType;
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
      if (connectionPropertiesType == (Type) null)
        throw new ArgumentNullException(nameof (connectionPropertiesType));
      this._connectionPropertiesTypes = (IDictionary<string, Type>) new Dictionary<string, Type>();
      this._connectionPropertiesTypes.Add(string.Empty, connectionPropertiesType);
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
      if (connectionUIControlType == (Type) null)
        throw new ArgumentNullException(nameof (connectionUIControlType));
      this._connectionUIControlTypes = (IDictionary<string, Type>) new Dictionary<string, Type>();
      this._connectionUIControlTypes.Add(string.Empty, connectionUIControlType);
    }

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
      this._connectionUIControlTypes = connectionUIControlTypes;
    }

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
      this._dataSourceDescriptions = dataSourceDescriptions;
    }

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
      this._dataSourceDescriptions = dataSourceDescriptions;
      this._connectionUIControlTypes = connectionUIControlTypes;
      this._connectionPropertiesTypes = connectionPropertiesTypes;
    }

    public static DataProvider SqlDataProvider
    {
      get
      {
        if (DataProvider._sqlDataProvider == null)
          DataProvider._sqlDataProvider = new DataProvider("Microsoft.Data.SqlClient", SR.DataProvider_Sql, SR.DataProvider_Sql_Short, SR.DataProvider_Sql_Description, typeof (SqlConnection), (IDictionary<string, string>) new Dictionary<string, string>()
          {
            {
              DataSource.SqlDataSource.Name,
              SR.DataProvider_Sql_DataSource_Description
            },
            {
              DataSource.SqlFileDataSource.Name,
              SR.DataProvider_Sql_FileDataSource_Description
            }
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
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
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
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
          DataProvider._oracleDataProvider = new DataProvider("System.Data.OracleClient", SR.DataProvider_Oracle, SR.DataProvider_Oracle_Short, SR.DataProvider_Oracle_Description, typeof (OracleConnection), (IDictionary<string, string>) new Dictionary<string, string>()
          {
            {
              DataSource.OracleDataSource.Name,
              SR.DataProvider_Oracle_DataSource_Description
            }
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
          {
            {
              string.Empty,
              typeof (OracleConnectionUIControl)
            }
          }, typeof (OracleConnectionProperties));
        return DataProvider._oracleDataProvider;
      }
    }

    public static DataProvider OleDBDataProvider
    {
      get
      {
        if (DataProvider._oleDBDataProvider == null)
          DataProvider._oleDBDataProvider = new DataProvider("System.Data.OleDb", SR.DataProvider_OleDB, SR.DataProvider_OleDB_Short, SR.DataProvider_OleDB_Description, typeof (OleDbConnection), (IDictionary<string, string>) new Dictionary<string, string>()
          {
            {
              DataSource.SqlDataSource.Name,
              SR.DataProvider_OleDB_SqlDataSource_Description
            },
            {
              DataSource.OracleDataSource.Name,
              SR.DataProvider_OleDB_OracleDataSource_Description
            },
            {
              DataSource.AccessDataSource.Name,
              SR.DataProvider_OleDB_AccessDataSource_Description
            }
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
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
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
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
          DataProvider._odbcDataProvider = new DataProvider("System.Data.Odbc", SR.DataProvider_Odbc, SR.DataProvider_Odbc_Short, SR.DataProvider_Odbc_Description, typeof (OdbcConnection), (IDictionary<string, string>) new Dictionary<string, string>()
          {
            {
              DataSource.OdbcDataSource.Name,
              SR.DataProvider_Odbc_DataSource_Description
            }
          }, (IDictionary<string, Type>) new Dictionary<string, Type>()
          {
            {
              string.Empty,
              typeof (OdbcConnectionUIControl)
            }
          }, typeof (OdbcConnectionProperties));
        return DataProvider._odbcDataProvider;
      }
    }

    public string Name => this._name;

    public string DisplayName => this._displayName == null ? this._name : this._displayName;

    public string ShortDisplayName => this._shortDisplayName;

    public string Description => this.GetDescription((DataSource) null);

    public Type TargetConnectionType => this._targetConnectionType;

    public virtual string GetDescription(DataSource dataSource)
    {
      return this._dataSourceDescriptions != null && dataSource != null && this._dataSourceDescriptions.ContainsKey(dataSource.Name) ? this._dataSourceDescriptions[dataSource.Name] : this._description;
    }

    public IDataConnectionUIControl CreateConnectionUIControl()
    {
      return this.CreateConnectionUIControl((DataSource) null);
    }

    public virtual IDataConnectionUIControl CreateConnectionUIControl(DataSource dataSource)
    {
      string key;
      return this._connectionUIControlTypes != null && dataSource != null && this._connectionUIControlTypes.ContainsKey(key = dataSource.Name) || this._connectionUIControlTypes.ContainsKey(key = string.Empty) ? Activator.CreateInstance(this._connectionUIControlTypes[key]) as IDataConnectionUIControl : (IDataConnectionUIControl) null;
    }

    public IDataConnectionProperties CreateConnectionProperties()
    {
      return this.CreateConnectionProperties((DataSource) null);
    }

    public virtual IDataConnectionProperties CreateConnectionProperties(DataSource dataSource)
    {
      string key;
      return this._connectionPropertiesTypes != null && dataSource != null && this._connectionPropertiesTypes.ContainsKey(key = dataSource.Name) || this._connectionPropertiesTypes.ContainsKey(key = string.Empty) ? Activator.CreateInstance(this._connectionPropertiesTypes[key]) as IDataConnectionProperties : (IDataConnectionProperties) null;
    }
  }
}
