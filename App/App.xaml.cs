using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace JocysCom.VS.AiCompanion
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			SetDPIAware();
			System.Windows.Forms.Application.EnableVisualStyles();
			System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
			TrayManager = new TrayManager();
			TrayManager.OnExitClick += TrayManager_OnExitClick;
		}

		private void TrayManager_OnExitClick(object sender, EventArgs e)
		{
			// Remove tray icon first.
			TrayManager.Dispose();
			Shutdown();
		}

		public TrayManager TrayManager { get; set; }

		internal class NativeMethods
		{
			[System.Runtime.InteropServices.DllImport("user32.dll")]
			internal static extern bool SetProcessDPIAware();
		}

		public static void SetDPIAware()
		{
			// DPI aware property must be set before application window is created.
			if (Environment.OSVersion.Version.Major >= 6)
				NativeMethods.SetProcessDPIAware();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			try
			{
				Global.GetClipboard = AppHelper.GetClipboard;
				Global.SetClipboard = AppHelper.SetClipboard;
				var window = new Engine.MainWindow();
				TrayManager.SetSettigns(Global.AppSettings);
				TrayManager.CreateTrayIcon();
				TrayManager.SetTrayFromWindow(window);
				TrayManager.SetWindow(window);
				// Create an instance of the MainWindow from the referenced library
				// Set it as the main window and show it
				MainWindow = window;
				TrayManager.ProcessGetCommandLineArgs();
			}
			catch (Exception ex)
			{
				if (IsDebug)
					throw;
				var message = ExceptionToText(ex);
				var result = MessageBox.Show(message, "Exception!", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK);
				if (result == MessageBoxResult.Cancel)
					Shutdown();
			}
		}

		public static bool IsDebug
		{
			get
			{
#if DEBUG
				return true;
#else
				return false;
#endif
			}

		}

		#region ■ ExceptionToText

		// Exception to string needed here so that links to other references won't be an issue.

		static string ExceptionToText(Exception ex)
		{
			var message = "";
			AddExceptionMessage(ex, ref message);
			if (ex.InnerException != null) AddExceptionMessage(ex.InnerException, ref message);
			return message;
		}

		/// <summary>Add information about missing libraries and DLLs</summary>
		static void AddExceptionMessage(Exception ex, ref string message)
		{
			var ex1 = ex as ConfigurationErrorsException;
			var ex2 = ex as ReflectionTypeLoadException;
			var m = "";
			if (ex1 != null)
			{
				m += string.Format("FileName: {0}\r\n", ex1.Filename);
				m += string.Format("Line: {0}\r\n", ex1.Line);
			}
			else if (ex2 != null)
			{
				foreach (Exception x in ex2.LoaderExceptions) m += x.Message + "\r\n";
			}
			if (message.Length > 0)
			{
				message += "===============================================================\r\n";
			}
			message += ex.ToString() + "\r\n";
			foreach (var key in ex.Data.Keys)
			{
				m += string.Format("{0}: {1}\r\n", key, ex1.Data[key]);
			}
			if (m.Length > 0)
			{
				message += "===============================================================\r\n";
				message += m;
			}
		}

		#endregion


	}
}

