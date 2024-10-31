using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Helper class for performing update-related processes such as replacing files and restarting the application.
	/// </summary>
	public class UpdateProcessHelper
	{
		/// <summary>
		/// Replaces the existing executable with a new version.
		/// </summary>
		/// <param name="bakFile">The backup file path of the current executable.</param>
		/// <param name="newFile">The file path of the new executable.</param>
		/// <param name="exeFile">The existing executable file path to be replaced.</param>
		/// <returns>"OK" if successful; otherwise, an error message.</returns>
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

		/// <summary>
		/// Restarts the application by closing existing instances and launching a new instance of the executable.
		/// </summary>
		/// <param name="exeFile">The executable file path to start.</param>
		/// <returns>"OK" if successful; otherwise, an error message.</returns>
		public static string RestartApp(string exeFile)
		{
			try
			{
				Task.Delay(1000).Wait();
				var currentProcess = Process.GetCurrentProcess();
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
				Process.Start(exeFile);
				// 4. Shutdown the current app.
				Environment.Exit(0);
				return "OK";
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show(ex.ToString());
				return ex.Message;
			}
		}

		/// <summary>
		/// Runs a command with elevated privileges if necessary.
		/// </summary>
		/// <param name="args">The arguments for the command to be executed.</param>
		/// <returns>True if the command was executed locally; false if a new process was started.</returns>
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
				JocysCom.ClassLibrary.Windows.UacHelper.RunProcess(
					JocysCom.ClassLibrary.Windows.UacHelper.CurrentProcessFileName,
					string.Join(" ", args), isElevated: true);
				return false;
			}
		}

		/// <summary>
		/// Runs a process asynchronously without waiting for it to finish.
		/// </summary>
		/// <param name="args">The arguments for the process to be executed.</param>
		/// <returns>True when the process starts successfully.</returns>
		public static bool RunProcessAsync(string[] args)
		{
			// Don't wait for process to finish.
			JocysCom.ClassLibrary.Windows.UacHelper.RunProcessAsync(
				JocysCom.ClassLibrary.Windows.UacHelper.CurrentProcessFileName,
				string.Join(" ", args));
			return true;
		}

		/// <summary>
		/// Processes administrative commands based on the provided arguments.
		/// </summary>
		/// <param name="args">The arguments containing the commands to execute.</param>
		/// <returns>True if a command was recognized and processed; otherwise, false.</returns>
		public static bool ProcessAdminCommands(string[] args)
		{
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
				var exeFile = ic["exeFile"];
				RestartApp(exeFile);
				return true;
			}
			return false;
		}
	}
}
