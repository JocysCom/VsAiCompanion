using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.ClassLibrary.Windows;
using JocysCom.VS.AiCompanion.Engine;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace JocysCom.VS.AiCompanion
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		bool allowOnlyOneCopy;
		WindowState windowState;

		public App()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute))).Product;
			try
			{
				// ------------------------------------------------
				// Administrator commands.
				// ------------------------------------------------
				var args = Environment.GetCommandLineArgs();
				var executed = JocysCom.ClassLibrary.Controls.UpdateControl.UpdateProcessHelper.ProcessAdminCommands(args);
				// If valid command was executed then...
				if (executed)
					return;
				// ------------------------------------------------
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
				FastReadAppSettings(out allowOnlyOneCopy, out windowState);
				allowToRun = GetAllowToRun();
				if (!allowToRun)
					return;
				SetDPIAware();
				System.Windows.Forms.Application.EnableVisualStyles();
				System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
				// Create tray manager first (initialize new default window).
				TrayManager = new TrayManager();
				Global.TrayManager = TrayManager;
				TrayManager.OnExitClick += TrayManager_OnExitClick;
			}
			catch (Exception ex)
			{
				var message = ExceptionToText(ex);
				var result = MessageBox.Show(message, $"{product} - Exception!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
				throw;
			}
		}

		private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			var s = $"TaskScheduler_UnobservedTaskException\r\n{e.Exception}";
			//MessageBox.Show(s);
			//System.Diagnostics.Debug.WriteLine(s);
			//System.Console.WriteLine(s);
		}

		private void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			var s = $"CurrentDomain_FirstChanceException\r\n{e.Exception}";
			//MessageBox.Show(s);
			//System.Diagnostics.Debug.WriteLine(s);
			//System.Console.WriteLine(s);

		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var s = $"CurrentDomain_UnhandledException\r\n{e.ExceptionObject}";
			//MessageBox.Show(s);
			//System.Diagnostics.Debug.WriteLine(s);
			//System.Console.WriteLine(s);
		}

		private async void Global_Tasks_Items_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			var updateTrayMenu =
				e.ListChangedType == System.ComponentModel.ListChangedType.ItemAdded ||
				e.ListChangedType == System.ComponentModel.ListChangedType.ItemDeleted ||
				(e.ListChangedType == System.ComponentModel.ListChangedType.ItemChanged &&
				e.PropertyDescriptor?.Name == nameof(TemplateItem.Icon));
			if (updateTrayMenu)
				await Helper.Debounce(UpdateTrayMenu);
		}
		private CancellationTokenSource cts = new CancellationTokenSource();

		private async void Global_AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.MaxTaskItemsInTray))
				await Helper.Debounce(UpdateTrayMenu);
		}

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
				var tasks = Global.Tasks.Items
					.OrderByDescending(x => x.Modified)
					.Take(Global.AppSettings.MaxTaskItemsInTray)
					.OrderByDescending(x => x.Name)
					.ToArray();
				foreach (var task in tasks)
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
			Global.MainControl.TasksPanel.TemplateItemPanel.ChatPanel.FocusChatInputTextBox();
		}

		private void StartHelper_OnRestore(object sender, EventArgs e)
		{
			TrayManager.RestoreFromTray();
		}

		private void StartHelper_OnClose(object sender, EventArgs e)
		{
			ShutDownWithOptionalSaveAndTrayDispose(true);
		}

		private bool allowToRun;

		private void TrayManager_OnExitClick(object sender, EventArgs e)
		{
			ShutDownWithOptionalSaveAndTrayDispose(true);
		}

		void ShutDownWithOptionalSaveAndTrayDispose(bool saveSettings)
		{
			if (saveSettings)
				Global.SaveSettings();
			// Remove tray icon first.
			TrayManager?.Dispose();
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

		public void LoadMainWindow()
		{
			// Error in this can casue Message box display whith will call OnStartup(StartupEventArgs e)
			// Which use tray manager.
			Global.LoadSettings();
			StartHelper.OnClose += StartHelper_OnClose;
			StartHelper.OnRestore += StartHelper_OnRestore;
			// ----------------------------------------------
			Global.GetClipboard = AppHelper.GetClipboard;
			Global.SetClipboard = AppHelper.SetClipboard;
			ClipboardHelper.XmlToColorizedHtml = AppHelper.XmlToColorizedHtml;
			var window = new Engine.MainWindow();
			TrayManager.SetSettigns(Global.AppSettings);
			TrayManager.CreateTrayIcon();
			TrayManager.SetTrayFromWindow(window);
			TrayManager.SetWindow(window);
			// Create an instance of the MainWindow from the referenced library
			// Set it as the main window and show it
			MainWindow = window;
			Global.OnMainControlLoaded += Global_OnMainControlLoaded;
			TrayManager.ProcessGetCommandLineArgs();
			Global.Tasks.Items.ListChanged += Global_Tasks_Items_ListChanged;
			Global.AppSettings.PropertyChanged += Global_AppSettings_PropertyChanged;
			_ = UpdateTrayMenu();
		}

		private void Global_OnMainControlLoaded(object sender, EventArgs e)
		{
			CloseSplashScreen();
		}

		#region Splash Screen


		private static Thread _splashThread;
		private static SplashScreenWindow _splashScreen;

		private void StartSplashScreen()
		{
			_splashScreen = new SplashScreenWindow();
			_splashScreen.Show();
			// DispatcherFrame approach for cleanly exiting
			System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
			_splashScreen.Closed += (s, e) => frame.Continue = false;
			System.Windows.Threading.Dispatcher.PushFrame(frame);
			// Start the dispatcher processing
			System.Windows.Threading.Dispatcher.Run();
		}

		private void CloseSplashScreen()
		{
			if (_splashScreen != null)
			{
				var operation = _splashScreen.Dispatcher.BeginInvoke(new Action(() =>
				{
					_splashScreen.Close();
					System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
					{
						System.Windows.Threading.Dispatcher.ExitAllFrames();
					}), System.Windows.Threading.DispatcherPriority.Background);
				}));
				// Comment this line to prevent the app from freezing while in debugging.
				//_splashThread.Join();
				_splashThread = null;
			}
		}

		#endregion

		protected override void OnStartup(StartupEventArgs e)
		{
			if (!allowToRun)
			{
				ShutDownWithOptionalSaveAndTrayDispose(false);
				return;
			}
			base.OnStartup(e);
			try
			{
				if (windowState != WindowState.Minimized)
				{
					// Start the splash screen on a separate thread
					_splashThread = new Thread(StartSplashScreen);
					_splashThread.SetApartmentState(ApartmentState.STA);
					_splashThread.Start();
				}
				LoadMainWindow();
			}
			catch (Exception ex)
			{
				if (IsDebug)
					throw;
				var message = ExceptionToText(ex);
				var result = MessageBox.Show(message, "Exception!", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK);
				if (result == MessageBoxResult.Cancel)
					ShutDownWithOptionalSaveAndTrayDispose(false);
			}
		}

		#region Allow To Run Check

		private static string GetHashString(string s)
		{
			using (var algorithm = System.Security.Cryptography.SHA256.Create())
			{
				var bytes = System.Text.Encoding.UTF8.GetBytes(s);
				var hash = algorithm.ComputeHash(bytes);
				var hashString = string.Join("", hash.Select(x => x.ToString("X2")));
				return hashString;
			}
		}

		private bool GetAllowToRun()
		{
			var modulepath = AssemblyInfo.Entry.ModuleBasePath;
			// Make sure name won't allow access same settings from multiple apps.
			var name = Directory.Exists(modulepath)
				// Application will use settings in same folder.
				? GetHashString(AssemblyInfo.Entry.ModuleBasePath)
				// Application use roaming settings.
				// Set unique id for broadcast to "JocysCom.VS.AiCompanion.App".
				: typeof(App).Assembly.GetName().Name;
			StartHelper.Initialize(name);
			// Check if another copy of application is already running.
			// Also execute commands if any.
			return StartHelper.AllowToRun(allowOnlyOneCopy);
		}

		/// <summary>
		/// Fast way to read `AllowOnlyOneCopy` value from settings.
		/// </summary>
		private static void FastReadAppSettings(out bool allowOnlyOneCopy, out WindowState windowState)
		{
			var oneCopyName = nameof(AppData.AllowOnlyOneCopy);
			var windowStateName = nameof(AppData.StartPosition.WindowState);
			var file = Global.AppData.XmlFile.FullName;
			allowOnlyOneCopy = Attributes.GetDefaultValue<AppData, bool>(oneCopyName);
			windowState = Attributes.GetDefaultValue<PositionSettings, WindowState>(windowStateName);
			if (!File.Exists(file))
				return;
			using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var reader = XmlReader.Create(stream))
				while (reader.Read())
				{
					if (reader.NodeType == XmlNodeType.Element && reader.Name == oneCopyName)
						if (reader.Read() && reader.NodeType == XmlNodeType.Text && bool.TryParse(reader.Value, out bool result))
							allowOnlyOneCopy = result;
					if (reader.NodeType == XmlNodeType.Element && reader.Name == windowStateName)
						if (reader.Read() && reader.NodeType == XmlNodeType.Text && WindowState.TryParse(reader.Value, out WindowState result))
							windowState = result;

				}
		}

		#endregion

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

