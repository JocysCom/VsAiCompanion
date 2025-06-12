using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Class to store assembly information with detection timestamp
	/// </summary>
	public class AssemblyRecord
	{
		public DateTime DetectedAt { get; set; }
		public string Name { get; set; }
		public string Version { get; set; }
		public string Location { get; set; }

		public AssemblyRecord(DateTime detectedAt, string name, string version, string location)
		{
			DetectedAt = detectedAt;
			Name = name;
			Version = version;
			Location = location;
		}
	}

	/// <summary>
	/// Interaction logic for ErrorsControl.xaml
	/// </summary>
	public partial class ErrorsControl : UserControl
	{
		public ErrorsControl()
		{
			InitializeComponent();
			if (InitHelper.IsDebug)
			{
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			}

			// Subscribe to refresh event for assemblies log panel
			AssembliesLogPanel.RefreshRequested += AssembliesLogPanel_RefreshRequested;
		}

		#region Exceptions

		List<(DateTime, Exception)> ExceptionsToDisplay = new List<(DateTime, Exception)>();

		public void WriteException(Exception ex)
		{
			if (Dispatcher.HasShutdownStarted)
				return;
			// Use `BeginInvoke, becase `Invoke` would freeze here.
			ControlsHelper.BeginInvoke(() =>
			{
				lock (ExceptionsToDisplay)
				{
					while (ExceptionsToDisplay.Count > 6)
						ExceptionsToDisplay.RemoveAt(ExceptionsToDisplay.Count - 1);
					var te = (DateTime.Now, ex);
					ExceptionsToDisplay.Insert(0, te);
					var strings = ExceptionsToDisplay
						.Select(x => $"---- {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {new string('-', 64)}\r\n{ex}\r\b")
						.ToList();
					ErrorsLogPanel.Clear();
					ErrorsLogPanel.Add(string.Join("\r\n", strings));
				}
				;
			});
		}

		public void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e is null)
				return;
			WriteException((Exception)e.ExceptionObject);
		}

		public void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			if (e is null)
				return;
			WriteException(e.Exception);
		}

		/// <summary>
		/// This is a "first chance exception", which means the debugger is simply notifying you
		/// that an exception was thrown, rather than that one was not handled.
		/// </summary>
		public void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			if (e is null || e.Exception is null)
				return;
			WriteException(e.Exception);
		}

		#endregion


		#region Assemblies

		bool isRunning;
		private readonly List<AssemblyRecord> _assemblyRecords = new List<AssemblyRecord>();

		/// <summary>
		/// Event handler for refresh request from assemblies log panel
		/// </summary>
		private void AssembliesLogPanel_RefreshRequested(object sender, EventArgs e)
		{
			if (!isRunning)
				ListAssemblies();
		}

		public async void ListAssemblies()
		{
			if (isRunning)
				return;
			isRunning = true;
			AssembliesLogPanel.Clear();
			AssembliesLogPanel.Add("Loading...\r\n");

			// Run the assembly enumeration on a background thread
			await Task.Run(() =>
			{
				var currentDateTime = DateTime.Now;
				var assemblies = AppDomain.CurrentDomain
					.GetAssemblies()
					.OrderBy(x => x.GetName().Name);

				// Check for new assemblies and add them to our records
				foreach (Assembly assembly in assemblies)
				{
					try
					{
						var an = assembly.GetName();
						var name = an.Name ?? "Unknown";
						var version = an.Version?.ToString() ?? "Unknown";
						var location = assembly.Location ?? "Unknown";

						// Check if this assembly is already in our records
						var existingRecord = _assemblyRecords.FirstOrDefault(r =>
							r.Name == name && r.Version == version && r.Location == location);

						if (existingRecord == null)
						{
							// New assembly detected, add it with current timestamp
							var newRecord = new AssemblyRecord(currentDateTime, name, version, location);
							_assemblyRecords.Add(newRecord);
						}
					}
					catch (Exception ex)
					{
						AssembliesLogPanel.Add($"ERROR processing assembly: {ex.Message}\r\n");
					}
				}

				// Clear and display all records
				AssembliesLogPanel.Clear();
				AssembliesLogPanel.Add($"DateTime Added\t\t{"Name",-48}\t\t\t{"Version",-10}\t\tLocation\r\n");
				AssembliesLogPanel.Add(new string('-', 120) + "\r\n");

				// Sort by detection time (newest first) then by name
				var sortedRecords = _assemblyRecords
					.OrderByDescending(r => r.DetectedAt)
					.ThenBy(r => r.Name);

				foreach (var record in sortedRecords)
				{
					var dateTimeStr = record.DetectedAt.ToString("yyyy-MM-dd HH:mm:ss");
					AssembliesLogPanel.Add($"{dateTimeStr}\t{record.Name,-48}\t{record.Version,-10}\t{record.Location}\r\n");
				}

				AssembliesLogPanel.Add($"\r\nTotal assemblies: {_assemblyRecords.Count}\r\n");
				AssembliesLogPanel.Add("Done\r\n");
				isRunning = false;
			});
		}

		#endregion

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (MainTabControl.SelectedItem == AssembliesTabItem)
			{
				if (!isRunning)
					ListAssemblies();
			}
		}
	}
}
