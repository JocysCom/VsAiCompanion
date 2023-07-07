using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine;
using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
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
			Global.LoadSettings();
			Global.InitDefaultSettings();
			// Set unique id for broadcast to "JocysCom.VS.AiCompanion.App".
			StartHelper.Initialize(typeof(App).Assembly.GetName().Name);
			allowToRun = StartHelper.AllowToRun(Global.AppSettings.AllowOnlyOneCopy);
			if (!allowToRun)
				return;
			StartHelper.OnClose += StartHelper_OnClose;
			StartHelper.OnRestore += StartHelper_OnRestore;
			SetDPIAware();
			System.Windows.Forms.Application.EnableVisualStyles();
			System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
			TrayManager = new TrayManager();
			TrayManager.OnExitClick += TrayManager_OnExitClick;
		}

		private async void Items_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			var updateTrayMenu =
				e.ListChangedType == System.ComponentModel.ListChangedType.ItemAdded ||
				e.ListChangedType == System.ComponentModel.ListChangedType.ItemDeleted ||
				(e.ListChangedType == System.ComponentModel.ListChangedType.ItemChanged &&
				e.PropertyDescriptor?.Name == nameof(TemplateItem.Icon));
			if (updateTrayMenu)
				await UpdateTrayMenu();
		}
		private CancellationTokenSource cts = new CancellationTokenSource();

		public async Task UpdateTrayMenu()
		{
			// Cancel any previous filter operation.
			cts.Cancel();
			cts = new CancellationTokenSource();
			await Task.Delay(500);
			// If new filter operation was started then return.
			if (cts.Token.IsCancellationRequested)
				return;
			lock (TrayManager)
			{

				// Cleanup first.
				var items = TrayManager.TrayMenuStrip.Items;
				foreach (System.Windows.Forms.ToolStripItem item in items.Cast<System.Windows.Forms.ToolStripItem>().ToArray())
				{
					if (item.Tag is TemplateItem)
					{
						item.Click -= MenuItem_Click;
						items.Remove(item);
					}
				}
				if (items[0].Name != "TasksSeparator")
				{
					var separator = new System.Windows.Forms.ToolStripSeparator();
					separator.Name = "TasksSeparator";
					items.Insert(0, separator);
				}
				// Reverse order to make sure that insert is done in alphabetically.
				foreach (var task in Global.Tasks.Items.Reverse())
				{
					var menuItem = new System.Windows.Forms.ToolStripMenuItem();
					menuItem.Text = task.Name;
					menuItem.Tag = task;
					menuItem.Image = AppHelper.ConvertDrawingImageToDrawingBitmap(task.Icon, 32, 32);
					menuItem.Click += MenuItem_Click;
					items.Insert(0, menuItem);
				}
			}
		}

		private void MenuItem_Click(object sender, EventArgs e)
		{
			var menuItem = (System.Windows.Forms.ToolStripMenuItem)sender;
			var item = menuItem.Tag as TemplateItem;
			TrayManager.RestoreFromTray();
			Global.MainControl.MainTabControl.SelectedItem = Global.MainControl.TasksTabItem;
			Global.MainControl.TasksPanel.ListPanel.SelectByName(item.Name);
			Dispatcher.BeginInvoke(new Action(() =>
			{
				var textBox = Global.MainControl.TasksPanel.ItemPanel.ChatPanel.DataTextBox;
				textBox.Focus();
				textBox.SelectionStart = textBox.Text?.Length ?? 0;
			}));
		}

		private void StartHelper_OnRestore(object sender, EventArgs e)
		{
			TrayManager.RestoreFromTray();
		}

		private void StartHelper_OnClose(object sender, EventArgs e)
		{
			Shutdown();
		}

		private bool allowToRun;

		private void TrayManager_OnExitClick(object sender, EventArgs e)
		{
			// Remove tray icon first.
			Global.SaveSettings();
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
			if (!allowToRun)
			{
				Shutdown();
				return;
			}
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
				Global.Tasks.Items.ListChanged += Items_ListChanged;
				_ = UpdateTrayMenu();
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

