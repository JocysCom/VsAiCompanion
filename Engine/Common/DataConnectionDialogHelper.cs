using System;
using Microsoft.Data.ConnectionUI;
#if NETFRAMEWORK
using System.Data.Entity;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.SqlClient;
using System.Data.SQLite.EF6;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
			DataSource.AddStandardDataSources(dcd);
			dcd.DataSources.Add(DataSource.SqlDataSource);
			dcd.SelectedDataProvider = DataProvider.SqlDataProvider;
			dcd.SelectedDataSource = DataSource.SqlDataSource;
			//			}
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

		/*
		public void OpenDatabaseConnectionDialog()
		{
			string filePath = Path.GetTempFileName() + ".udl";
			Process.Start(filePath);
		}

		public void MonitorFileChange()
		{
			string filePath = Path.GetTempFileName() + ".udl";
			using (var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath), "*.udl"))
			{
				watcher.NotifyFilter = NotifyFilters.LastWrite;
				watcher.Changed += (object sender, FileSystemEventArgs e) =>
			{
					if (e.FullPath == filePath)
					{
						string[] lines = File.ReadAllLines(filePath);
						foreach (string line in lines)
						{
							if (line.StartsWith("Provider="))
							{
								string connectionString = line.Substring("Provider=".Length);
								// use the connection string
								Debug.WriteLine(connectionString);
							}
						}
					}
				};
				watcher.EnableRaisingEvents = true;
				OpenDatabaseConnectionDialog(filePath);
				Console.ReadLine();
			}
		}
		*/

	}
}
