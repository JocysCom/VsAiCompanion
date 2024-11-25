using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Windows;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.VS.AiCompanion.Extension.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using Microsoft.VisualStudio.Shell;
using System.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Extension
{
	[Guid("9a8e8fad-eb2a-f9f6-abb1-1faa8d7fdec2")]
	public class MainWindow : ToolWindow
	{
		private SplashScreenControl _splashScreenPanel;

		public override ToolWindowConfiguration ToolWindowConfiguration => new()
		{
			Placement = ToolWindowPlacement.Floating,
			DockDirection = Dock.Right,
			AllowAutoCreation = true,
		};

		public MainWindow()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute))).Product;
			try
			{
				// Set assembly info manually because in Visual Studio it crashes when determining automatically.
				JocysCom.ClassLibrary.Configuration.AssemblyInfo.Entry = new JocysCom.ClassLibrary.Configuration.AssemblyInfo(assembly);
				// Set caption.
				this.Title = product;
				_splashScreenPanel = new SplashScreenControl();
				_splashScreenPanel.LogTextBox.Visibility = Visibility.Collapsed;
				_splashScreenPanel.Loaded += Splash_Loaded;
				//this.ContentControl = _splashScreenPanel;
			}
			catch (Exception ex)
			{
				var message = ExceptionToText(ex);
				_splashScreenPanel.LoadingPanel.Visibility = Visibility.Collapsed;
				_splashScreenPanel.LogTextBox.Text = message;
				_splashScreenPanel.LogTextBox.Visibility = Visibility.Visible;
			}
		}

		public override async Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
		{
			await LoadMainControlAsync();
			return (IRemoteUserControl)Global.MainControl;
		}

		private void Splash_Loaded(object sender, RoutedEventArgs e)
		{
			_splashScreenPanel.Loaded -= Splash_Loaded;
			_ = Helper.Debounce(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				try
				{
					await LoadMainControlAsync();
					// Ensure that the content switch happens on the main thread.
					_splashScreenPanel.MainBorder.Child = Global.MainControl;
				}
				catch (Exception ex)
				{
					var message = ExceptionToText(ex);
					_splashScreenPanel.LogTextBox.Text = message;
					_splashScreenPanel.LogTextBox.Visibility = Visibility.Visible;
				}
				_splashScreenPanel.LoadingPanel.Visibility = Visibility.Collapsed;
			});
		}

		public async Task LoadMainControlAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			// Subscribe to the AssemblyResolve event. This event is triggered when .NET runtime fails to find an assembly,
			// giving you an opportunity to provide the assembly using custom logic.
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			// This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
			// we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
			// the object returned by the Content property.
			ControlsHelper.InitInvokeContext();
			Global.LoadSettings();
			// Get or Set multiple documents.
			var solutionHelper = new SolutionHelper();
			Global._SolutionHelper = solutionHelper;
			Global.SwitchToVisualStudioThreadAsync = solutionHelper.SwitchToMainThreadAsync;

			VisualStudio.Current = Global._SolutionHelper;
			Global.IsVsExtension = true;
			Global.StartKeyboardHook();

			var vsContext = Global._SolutionHelper.GetEnvironmentContext();
			if (vsContext.ContainsKey("DTE Version"))
			{
				var versionString = vsContext["DTE Version"].Deserialize<string>();
				if (Version.TryParse(versionString, out var version))
				{
					Global.VsVersion = version;
					Global.ShowExtensionVersionMessageOnError = version < new Version(17, 9);
				}
			}
			Global.GetClipboard = AppHelper.GetClipboard;
			Global.SetClipboard = AppHelper.SetClipboard;
			Global.GetEnvironmentProperties = AppHelper.GetEnvironmentProperties;
			ClipboardHelper.XmlToColorizedHtml = AppHelper.XmlToColorizedHtml;
			Plugins.Core.ScreenshotHelper.GetTempFolderPath = AppHelper.GetTempFolderPath;
			// Create controls.
			var control = new Engine.MainControl();
			Global.MainControl = control;
		}

		private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
		{
			var dllName = new AssemblyName(e.Name).Name + ".dll";
			var assemblyLocation = Assembly.GetExecutingAssembly().Location;
			var assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);
			var assemblyPath = System.IO.Path.Combine(assemblyDirectory, dllName);
			if (System.IO.File.Exists(assemblyPath))
				return Assembly.LoadFrom(assemblyPath);
			return null;
		}

		static string ExceptionToText(Exception ex)
		{
			var message = "";
			AddExceptionMessage(ex, ref message);
			if (ex.InnerException != null) AddExceptionMessage(ex.InnerException, ref message);
			return message;
		}

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

	}
}
