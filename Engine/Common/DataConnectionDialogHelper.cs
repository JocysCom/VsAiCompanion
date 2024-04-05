using System.Collections.Generic;
using System;










#if NETFRAMEWORK
using Microsoft.Data.ConnectionUI;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.SqlClient;
using System.Data.SQLite.EF6;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Management.ConnectionUI;
#endif

namespace JocysCom.VS.AiCompanion.Engine
{
	public class DataConnectionDialogHelper
	{
		public static string ShowDialog(string connectionString)
		{
			var dcd = new DataConnectionDialog();
			//Adds all the standard supported databases
			dcd.DataSources.Clear();
			/*
						if ((connectionString ?? "").Contains(".db"))
						{
							connectionString = AssemblyInfo.ExpandPath(connectionString);
							connectionString = JocysCom.VS.AiCompanion.DataClient.SqliteHelper.NewConnection(connectionString).ConnectionString;
			#if NETFRAMEWORK
							var factory = SQLiteFactory.Instance;
							var name = factory.GetType().Namespace;
							var sqliteDataSource = new DataSource(nameof(System.Data.SQLite), "System Data SQLite");
			#else
							var factory = SqliteFactory.Instance;
							var name = factory.GetType().Namespace;
							var sqliteDataSource = new DataSource(nameof(Microsoft.Data.Sqlite), "Microsoft Data Sqlite");
			#endif
							//DataSource.AddStandardDataSources(dcd);
							var sqliteDataProvider = CreateSqliteDataProvider();
							sqliteDataSource.Providers.Add(sqliteDataProvider);
							dcd.DataSources.Add(sqliteDataSource);
							dcd.SelectedDataProvider = sqliteDataProvider;
							dcd.SelectedDataSource = sqliteDataSource;
						}
						else
						{
			*/
			dcd.DataSources.Add(DataSource.SqlDataSource);
			dcd.SelectedDataProvider = DataProvider.SqlDataProvider;
			dcd.SelectedDataSource = DataSource.SqlDataSource;
			//			}
			try
			{
				dcd.ConnectionString = connectionString ?? "";
			}
			catch { }
			DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
				return dcd.ConnectionString;
			return null;
		}


		public static DataProvider CreateSqliteDataProvider()
		{
			// Define the SQLite data source descriptions for basic identification.
			var dataSourceDescriptions = new Dictionary<string, string>
	{
		{ "SQLite Database", "A local SQLite database file" }
	};

			var connectionPropertiesTypes = new Dictionary<string, Type>
	{
        // The key aspect here is not to attempt sophisticated UI behavior,
        // but ensure essential connection string properties are recognized.
        // "Data Source" is the main property we're interested in for SQLite.
        { "Data Source", typeof(string) }
	};

			// Simplified provider name and display name, tailored for SQLite.
			string providerName = "SQLite";
			string providerDisplayName = "SQLite Database";

			// The type of connection is essential to match the SQLite's expected connection class.
#if NETFRAMEWORK
			Type connectionType = typeof(System.Data.SQLite.SQLiteConnection);
#else
			Type connectionType = typeof(Microsoft.Data.Sqlite.SqliteConnection);
#endif
			// Instantiate and return the DataProvider with just essential settings.
			var sqliteDataProvider = new DataProvider(
				providerName,
				providerDisplayName,
				"SQLite",
				"Data Provider for SQLite Databases",
				connectionType,
				dataSourceDescriptions,
				new Dictionary<string, Type>(),
				connectionPropertiesTypes
			);

			return sqliteDataProvider;
		}

	}
}
