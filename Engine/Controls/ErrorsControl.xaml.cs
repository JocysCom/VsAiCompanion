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
				};
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

		public async void ListAssemblies()
		{
			if (isRunning)
				return;
			isRunning = true;
			AssembliesLogPanel.Clear();
			AssembliesLogPanel.Add("Loading...");
			// Run the assembly enumeration on a background thread
			await Task.Run(() =>
			{
				var assemblies = AppDomain.CurrentDomain
					.GetAssemblies()
					.OrderBy(x => x.GetName().Name);

				foreach (Assembly assembly in assemblies)
				{
					try
					{


						var an = assembly.GetName();
						AssembliesLogPanel.Add($"{an.Name}\t{an.Version}\t{assembly.Location}\r\n");
					}
					catch (Exception ex)
					{
						AssembliesLogPanel.Add($"ERROR: {ex.Message}\r\n");
					}
				}
				AssembliesLogPanel.Add("Done");
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
