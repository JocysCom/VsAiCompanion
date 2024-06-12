using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.VS.AiCompanion.Plugins.Core;
using Microsoft.VisualStudio.Shell;
using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Extension
{
	/// <summary>
	/// This class implements the tool window exposed by this package and hosts a user control.
	/// </summary>
	/// <remarks>
	/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
	/// usually implemented by the package implementer.
	/// <para>
	/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
	/// implementation of the IVsUIElementPane interface.
	/// </para>
	/// Guid decorating this class must be UNIQUE. If two extensions have the same GUID for a tool window,
	/// Visual Studio might not be able to correctly identify and instantiate the tool windows, leading to conflicts.
	/// </remarks>
	[Guid("9a8e8fad-eb2a-f9f6-abb1-1faa8d7fdec2")]
	public class MainWindow : ToolWindowPane
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MainWindow"/> class.
		/// </summary>
		public MainWindow() : base(null)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute))).Product;
			try
			{
				// Set assembly info manually because in Visual Studio it crashes when determining automatically.
				JocysCom.ClassLibrary.Configuration.AssemblyInfo.Entry = new JocysCom.ClassLibrary.Configuration.AssemblyInfo(assembly);
				// Set caption.
				Caption = product;
				_SplashScreenPanel = new SplashScreenControl();
				_SplashScreenPanel.Loaded += Splash_Loaded;
				Content = _SplashScreenPanel;
			}
			catch (Exception ex)
			{
				var message = ExceptionToText(ex);
				//var result = MessageBox.Show(message, $"{product} - Exception!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
				Content = new System.Windows.Controls.TextBlock { Text = message };
			}
		}

		SplashScreenControl _SplashScreenPanel;

		private void Splash_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			_SplashScreenPanel.Loaded -= Splash_Loaded;
			_ = Helper.Delay(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				try
				{
					await LoadMainControlAsync();
					// Ensure that the content switch happens on the main thread.
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
					_SplashScreenPanel.MainTextBox.Text = Global.MainControl == null
						? "MainControl is null"
						: "MainControl is instantiated";
					_SplashScreenPanel.MainBorder.Child = Global.MainControl;
				}
				catch (Exception ex)
				{
					var message = ExceptionToText(ex);
					_SplashScreenPanel.MainTextBox.Text = message;
				}
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
			;
			var vsContext = Global._SolutionHelper.GetEnvironmentContext();
			if (vsContext.ContainsKey("DTE Version"))
			{
				var versionString = vsContext["DTE Version"].Deserialize<string>();
				Version version;
				if (Version.TryParse(versionString, out version))
				{
					Global.VsVersion = version;
					Global.ShowExtensionVersionMessageOnError = version < new Version(17, 9);
				}
			}
			Global.GetClipboard = AppHelper.GetClipboard;
			Global.SetClipboard = AppHelper.SetClipboard;
			Global.GetEnvironmentProperties = AppHelper.GetEnvironmentProperties;
			//Global.GetEnvironmentProperties = SolutionHelper.GetEnvironmentProperties;
			//Global.GetReservedProperties = SolutionHelper.GetReservedProperties;
			//Global.GetOtherProperties = SolutionHelper.GetOtherProperties;
			// Create controls.
			var control = new Engine.MainControl();
			Global.MainControl = control;
		}

		/// <summary>
		/// Helps .NET to find assemblies (*.DLLs) from the extension's installed folder.
		/// This method is invoked when .NET runtime fails to find an assembly it's looking for.
		/// </summary>
		/// <param name="sender">The source of the event, in this case, the current AppDomain.</param>
		/// <param name="e">The arguments for the resolve event, containing the name of the assembly that failed to load.</param>
		/// <returns>The resolved assembly, or null if the assembly could not be found.</returns>
		private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
		{
			// Extract the simple name of the assembly from the full assembly name.
			// Full assembly names are in the form 'SimpleName, Version, Culture, PublicKeyToken'
			var dllName = new AssemblyName(e.Name).Name + ".dll";
			// Get the path where the extension is installed (i.e., the path of the currently executing assembly).
			var assemblyLocation = Assembly.GetExecutingAssembly().Location;
			var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
			// Construct the full path to the assembly using the directory of the currently executing assembly and the dll name.
			var assemblyPath = Path.Combine(assemblyDirectory, dllName);
			// If the assembly exists at the constructed path, load and return it.
			if (File.Exists(assemblyPath))
				return Assembly.LoadFrom(assemblyPath);
			// If the assembly was not found in the specified directory, return null.
			// This lets the default assembly resolution process continue and .NET runtime will throw FileNotFoundException.
			return null;
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
