using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
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
			_displayName = SR.GetString("DataSource_UnspecifiedDisplayName");
			_providers = (ICollection<DataProvider>)new DataSource.DataProviderCollection(this);
		}

		public DataSource(string name, string displayName)
		{
			_name = name != null ? name : throw new ArgumentNullException(nameof(name));
			_displayName = displayName;
			_providers = (ICollection<DataProvider>)new DataSource.DataProviderCollection(this);
		}

#if NETCOREAPP
		[SupportedOSPlatform("windows")]
#endif
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
					DataSource._sqlDataSource = new DataSource("MicrosoftSqlServer", SR.GetString("DataSource_MicrosoftSqlServer"));
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
					DataSource._sqlFileDataSource = new DataSource("MicrosoftSqlServerFile", SR.GetString("DataSource_MicrosoftSqlServerFile"));
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
					DataSource._oracleDataSource = new DataSource("Oracle", SR.GetString("DataSource_Oracle"));
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
					DataSource._accessDataSource = new DataSource("MicrosoftAccess", SR.GetString("DataSource_MicrosoftAccess"));
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
					DataSource._odbcDataSource = new DataSource("OdbcDsn", SR.GetString("DataSource_MicrosoftOdbcDsn"));
					DataSource._odbcDataSource.Providers.Add(DataProvider.OdbcDataProvider);
				}
				return DataSource._odbcDataSource;
			}
		}

		public string Name => _name;

		public string DisplayName => _displayName == null ? _name : _displayName;

		public DataProvider DefaultProvider
		{
			get
			{
				switch (_providers.Count)
				{
					case 0:
						return (DataProvider)null;
					case 1:
						IEnumerator<DataProvider> enumerator = _providers.GetEnumerator();
						enumerator.MoveNext();
						return enumerator.Current;
					default:
						return _name == null ? (DataProvider)null : _defaultProvider;
				}
			}
			set
			{
				if (_providers.Count == 1 && _defaultProvider != value)
					throw new InvalidOperationException(SR.GetString("DataSource_CannotChangeSingleDataProvider"));
				_defaultProvider = value == null || _providers.Contains(value) ? value : throw new InvalidOperationException(SR.GetString("DataSource_DataProviderNotFound"));
			}
		}

		[CLSCompliant(false)]
		public ICollection<DataProvider> Providers => _providers;

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
				_list = (ICollection<DataProvider>)new List<DataProvider>();
				_source = source;
			}

			public int Count => _list.Count;

			public bool IsReadOnly => false;

			public void Add(DataProvider item)
			{
				if (item == null)
					throw new ArgumentNullException(nameof(item));
				if (_list.Contains(item))
					return;
				_list.Add(item);
			}

			public bool Contains(DataProvider item) => _list.Contains(item);

			public bool Remove(DataProvider item)
			{
				bool flag = _list.Remove(item);
				if (item == _source._defaultProvider)
					_source._defaultProvider = (DataProvider)null;
				return flag;
			}

			public void Clear()
			{
				_list.Clear();
				_source._defaultProvider = (DataProvider)null;
			}

			public void CopyTo(DataProvider[] array, int arrayIndex)
			{
				_list.CopyTo(array, arrayIndex);
			}

			public IEnumerator<DataProvider> GetEnumerator() => _list.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_list.GetEnumerator();
		}
	}
}
