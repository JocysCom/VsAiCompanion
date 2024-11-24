using System;
using System.Data.Common;
#if NETFRAMEWORK
using Microsoft.Data.ConnectionUI;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.ConnectionUI;
using JocysCom.VS.AiCompanion.DataClient;
//using Microsoft.SqlServer.Management.ConnectionUI;
#endif

namespace JocysCom.VS.AiCompanion.Engine
{
	public class DataConnectionDialogHelper
	{
		public static string ShowDialog(string connectionString)
		{
			var dcd = new DataConnectionDialog();
			dcd.DataSources.Clear();
#if NETFRAMEWORK
			dcd.DataSources.Add(DataSource.SqlDataSource);
			dcd.SelectedDataSource = DataSource.SqlDataSource;
			dcd.SelectedDataProvider = DataProvider.SqlDataProvider;
#else
			//DataSource.AddStandardDataSources(dcd);
			//SqlInitHelper.AddDbProviderFactory(System.Data.Odbc.OdbcFactory.Instance);
			//SqlInitHelper.AddDbProviderFactory(System.Data.OleDb.OleDbFactory.Instance);
			//SqlInitHelper.AddDbProviderFactory(System.Data.OracleClient.OracleClientFactory.Instance);
			SqlInitHelper.AddDbProviderFactory(Microsoft.Data.SqlClient.SqlClientFactory.Instance);
			dcd.DataSources.Add(DataSource.SqlDataSource);
			dcd.SelectedDataSource = DataSource.SqlDataSource;
			dcd.SelectedDataProvider = DataProvider.SqlDataProvider;
#endif
			try
			{
				dcd.ConnectionString = ReformatConnectionStringForSystemData(connectionString);
			}
			catch (Exception)
			{

			}
			DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
				return dcd.ConnectionString;
			return null;
		}

		private static void RegisterFactory(DbProviderFactory instance)
		{
#if NETFRAMEWORK
#else
			DbProviderFactories.RegisterFactory(instance.GetType().Namespace, instance);
#endif
		}

		private static string ReformatConnectionStringForSystemData(string connectionString)
		{
			string str = connectionString;
			str = str.Replace("Application Intent", "ApplicationIntent");
			str = str.Replace("Connect Retry Count", "ConnectRetryCount");
			str = str.Replace("Connect Retry Interval", "ConnectRetryInterval");
			str = str.Replace("Pool Blocking Period", "PoolBlockingPeriod");
			str = str.Replace("Multiple Active Result Sets", "MultipleActiveResultSets");
			str = str.Replace("Multiple Subnet Failover", "MultiSubnetFailover");
			str = str.Replace("Transparent Network IP Resolution", "TransparentNetworkIPResolution");
			str = str.Replace("Trust Server Certificate", "TrustServerCertificate");
			return str;
		}

	}
}
