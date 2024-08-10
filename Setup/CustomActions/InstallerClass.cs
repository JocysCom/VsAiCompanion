using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace CustomActions
{
	[RunInstaller(true)]
	public class InstallerClass : Installer
	{

		protected override void OnBeforeInstall(IDictionary savedState)
		{
			base.OnBeforeInstall(savedState);
			ExitRunningInstances();
		}

		protected override void OnBeforeUninstall(IDictionary savedState)
		{
			base.OnBeforeUninstall(savedState);
			ExitRunningInstances();
		}

		private void ExitRunningInstances([CallerMemberName] string memberName = "")
		{
			try
			{
				// The `\` at the end of /targetdir="[TARGETDIR]\" ensures that the path passed as targetdir parameter is correctly recognized as a directory path.
				// Without it, the setup might interpret the targetdir as an incomplete or incorrect path, leading to issues during execution.
				// Set the CustomActionData property to /targetdir="[TARGETDIR]\"
				var path1 = Context.Parameters["targetdir"].TrimEnd('\\');
				var path2 = "JocysCom.VS.AiCompanion.App.exe";
				var exePath = Path.Combine(path1, path2);
				AddLog(exePath, memberName);
				var fi = new FileInfo(exePath);
				if (fi.Exists)
				{
					ProcessStartInfo startInfo = new ProcessStartInfo
					{
						FileName = exePath,
						Arguments = "/Exit",
						UseShellExecute = false,
						CreateNoWindow = true
					};
					var proc = Process.Start(startInfo);
					proc.WaitForExit();
					AddLog("Closing", memberName);
					// Terminate process.
					foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fi.Name)))
					{
						AddLog($"Found running process: {process.ProcessName} (ID: {process.Id})", memberName);
						process.Kill();
						process.WaitForExit();
						AddLog("Process terminated successfully.", memberName);
					}
				}
			}
			catch (Exception ex)
			{
				AddLog(ex.ToString(), memberName);
				// Handle any exceptions if necessary
				throw new InstallException("Could not close running instances of the application.", ex);
			}
		}

		public static void AddLog(string message, string memberName = "")
		{
			//var logPath = "c:\\temp\\_setuplog.txt";
			//System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {memberName}: {message}\r\n");
		}
	}
}
