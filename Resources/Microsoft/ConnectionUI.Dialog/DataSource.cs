using System;
using System.Collections;
using System.Collections.Generic;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class DataSource
  {
    private static DataSource _sqlDataSource;
    private static DataSource _sqlFileDataSource;
    private static DataSource _oracleDataSource;
    private static DataSource _accessDataSource;
    private static DataSource _odbcDataSource;
    private string _name;
    private string _displayName;
    private DataProvider _defaultProvider;
    private ICollection<DataProvider> _providers;

    private DataSource()
    {
      this._displayName = SR.DataSource_UnspecifiedDisplayName;
      this._providers = (ICollection<DataProvider>) new DataSource.DataProviderCollection(this);
    }

    public DataSource(string name, string displayName)
    {
      this._name = name != null ? name : throw new ArgumentNullException(nameof (name));
      this._displayName = displayName;
      this._providers = (ICollection<DataProvider>) new DataSource.DataProviderCollection(this);
    }

    public static void AddStandardDataSources(DataConnectionDialog dialog)
    {
      dialog.DataSources.Add(DataSource.SqlDataSource);
      dialog.DataSources.Add(DataSource.SqlFileDataSource);
      dialog.DataSources.Add(DataSource.OracleDataSource);
      dialog.DataSources.Add(DataSource.AccessDataSource);
      dialog.DataSources.Add(DataSource.OdbcDataSource);
      dialog.UnspecifiedDataSource.Providers.Add(DataProvider.SqlDataProvider);
      dialog.UnspecifiedDataSource.Providers.Add(DataProvider.OracleDataProvider);
      dialog.UnspecifiedDataSource.Providers.Add(DataProvider.OleDBDataProvider);
      dialog.UnspecifiedDataSource.Providers.Add(DataProvider.OdbcDataProvider);
      dialog.DataSources.Add(dialog.UnspecifiedDataSource);
    }

    public static DataSource SqlDataSource
    {
      get
      {
        if (DataSource._sqlDataSource == null)
        {
          DataSource._sqlDataSource = new DataSource("MicrosoftSqlServer", SR.DataSource_MicrosoftSqlServer);
          DataSource._sqlDataSource.Providers.Add(DataProvider.SqlDataProvider);
          DataSource._sqlDataSource.Providers.Add(DataProvider.OleDBDataProvider);
        }
        return DataSource._sqlDataSource;
      }
    }

    public static DataSource SqlFileDataSource
    {
      get
      {
        if (DataSource._sqlFileDataSource == null)
        {
          DataSource._sqlFileDataSource = new DataSource("MicrosoftSqlServerFile", SR.DataSource_MicrosoftSqlServerFile);
          DataSource._sqlFileDataSource.Providers.Add(DataProvider.SqlDataProvider);
        }
        return DataSource._sqlFileDataSource;
      }
    }

    public static DataSource OracleDataSource
    {
      get
      {
        if (DataSource._oracleDataSource == null)
        {
          DataSource._oracleDataSource = new DataSource("Oracle", SR.DataSource_Oracle);
          DataSource._oracleDataSource.Providers.Add(DataProvider.OracleDataProvider);
          DataSource._oracleDataSource.Providers.Add(DataProvider.OleDBDataProvider);
        }
        return DataSource._oracleDataSource;
      }
    }

    public static DataSource AccessDataSource
    {
      get
      {
        if (DataSource._accessDataSource == null)
        {
          DataSource._accessDataSource = new DataSource("MicrosoftAccess", SR.DataSource_MicrosoftAccess);
          DataSource._accessDataSource.Providers.Add(DataProvider.OleDBDataProvider);
        }
        return DataSource._accessDataSource;
      }
    }

    public static DataSource OdbcDataSource
    {
      get
      {
        if (DataSource._odbcDataSource == null)
        {
          DataSource._odbcDataSource = new DataSource("OdbcDsn", SR.DataSource_MicrosoftOdbcDsn);
          DataSource._odbcDataSource.Providers.Add(DataProvider.OdbcDataProvider);
        }
        return DataSource._odbcDataSource;
      }
    }

    public string Name => this._name;

    public string DisplayName => this._displayName == null ? this._name : this._displayName;

    public DataProvider DefaultProvider
    {
      get
      {
        switch (this._providers.Count)
        {
          case 0:
            return (DataProvider) null;
          case 1:
            IEnumerator<DataProvider> enumerator = this._providers.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
          default:
            return this._name == null ? (DataProvider) null : this._defaultProvider;
        }
      }
      set
      {
        if (this._providers.Count == 1 && this._defaultProvider != value)
          throw new InvalidOperationException(SR.DataSource_CannotChangeSingleDataProvider);
        this._defaultProvider = value == null || this._providers.Contains(value) ? value : throw new InvalidOperationException(SR.DataSource_DataProviderNotFound);
      }
    }

    public ICollection<DataProvider> Providers => this._providers;

    internal static DataSource CreateUnspecified() => new DataSource();

    private class DataProviderCollection : 
      ICollection<DataProvider>,
      IEnumerable<DataProvider>,
      IEnumerable
    {
      private ICollection<DataProvider> _list;
      private DataSource _source;

      public DataProviderCollection(DataSource source)
      {
        this._list = (ICollection<DataProvider>) new List<DataProvider>();
        this._source = source;
      }

      public int Count => this._list.Count;

      public bool IsReadOnly => false;

      public void Add(DataProvider item)
      {
        if (item == null)
          throw new ArgumentNullException(nameof (item));
        if (this._list.Contains(item))
          return;
        this._list.Add(item);
      }

      public bool Contains(DataProvider item) => this._list.Contains(item);

      public bool Remove(DataProvider item)
      {
        int num = this._list.Remove(item) ? 1 : 0;
        if (item != this._source._defaultProvider)
          return num != 0;
        this._source._defaultProvider = (DataProvider) null;
        return num != 0;
      }

      public void Clear()
      {
        this._list.Clear();
        this._source._defaultProvider = (DataProvider) null;
      }

      public void CopyTo(DataProvider[] array, int arrayIndex)
      {
        this._list.CopyTo(array, arrayIndex);
      }

      public IEnumerator<DataProvider> GetEnumerator() => this._list.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this._list.GetEnumerator();
    }
  }
}
