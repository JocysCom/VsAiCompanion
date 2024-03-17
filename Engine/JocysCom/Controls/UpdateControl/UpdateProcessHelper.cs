using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	public class UpdateProcessHelper
	{

		public static string ReplaceFiles(string bakFile, string newFile, string exeFile)
		{
			try
			{
				if (!File.Exists(newFile))
					return $"New file is missing: {newFile}";
				if (!File.Exists(newFile))
					return $"Exe file is missing: {exeFile}";
				// Delete current application backup.
				if (File.Exists(bakFile))
					File.Delete(bakFile);
				File.Move(exeFile, bakFile);
				File.Move(newFile, exeFile);
				return "OK";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}
		public static string RestartApp()
		{
			try
			{
				var currentProcess = Process.GetCurrentProcess();
				var assembly = System.Reflection.Assembly.GetEntryAssembly();
				var location = assembly.Location;
				// 1. Close all other instances of the current app that have a different process ID (PID).
				var allProcesses = Process.GetProcessesByName(currentProcess.ProcessName);
				foreach (var process in allProcesses.Where(p => p.Id != currentProcess.Id))
				{
					//process.Kill(); // Or, use process.CloseMainWindow() for graceful termination
					process.WaitForExit(); // Ensure the process has exited
				}
				// 2. Wait for all other instances of the app to close.
				bool allClosed = false;
				while (!allClosed)
				{
					allClosed = Process.GetProcessesByName(currentProcess.ProcessName).All(p => p.Id == currentProcess.Id);
					Thread.Sleep(1000); // Wait a bit before checking again
				}
				// 3. Start the app at the specified location.
				Process.Start(location);
				// 4. Shutdown the current app.
				Environment.Exit(0);
				return "OK";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		/// <summary>
		/// Returns true if command was executed locally.
		/// </summary>
		public static bool RunElevated(string[] args)
		{

			// If program is running as Administrator already.
			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsElevated)
			{
				// Run command directly.
				ProcessAdminCommands(args);
				return true;
			}
			else
			{
				// Run copy of app as Administrator.
				JocysCom.ClassLibrary.Win32.UacHelper.RunProcess(
					System.Reflection.Assembly.GetEntryAssembly().Location,
					string.Join(" ", args), isElevated: true);
				return false;
			}
		}
		public static bool RunProcessAsync(string[] args)
		{
			// Don't wait for process to finish.
			JocysCom.ClassLibrary.Win32.UacHelper.RunProcessAsync(
				System.Reflection.Assembly.GetEntryAssembly().Location,
				string.Join(" ", args));
			return true;

		}
		public static bool ProcessAdminCommands(string[] args)
		{

			// Requires System.Configuration.Installl reference.
			var ic = new JocysCom.ClassLibrary.Configuration.Arguments(args);
			// ------------------------------------------------
			if (ic.ContainsKey(nameof(ReplaceFiles)))
			{
				var bakFile = ic["bakFile"];
				var newFile = ic["newFile"];
				var exeFile = ic["exeFile"];
				ReplaceFiles(bakFile, newFile, exeFile);
				return true;
			}
			// ------------------------------------------------
			if (ic.ContainsKey(nameof(RestartApp)))
			{
				RestartApp();
				return true;
			}
			return false;
		}

	}
}
