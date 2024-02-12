using System.Diagnostics;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Helps AI to execute applications and scripts.
	/// </summary>
	public class PowerShellExecutorHelper
	{

		/// <summary>
		/// Use to execute a process on user computer. Use `System.Diagnostics.ProcessStartInfo`
		/// </summary>
		/// <param name="fileName">Application or document to start.</param>
		/// <param name="arguments">Command-line arguments to use when starting the application.</param>
		/// <param name="workingDirectory">The working directory for the process to be started. Default is empty string.</param>
		/// <returns>The output of the started process.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		/// <remarks>Be cautious with executing process requesys received via API due to security risks.</remarks>
		public static string StartProcess(string fileName, string arguments, string workingDirectory)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;
				process.Start();
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return output;
			}
		}

		/// <summary>
		/// Use to execute a PowerShell script on user computer.
		/// </summary>
		/// <param name="script">The PowerShell script to be executed.</param>
		/// <returns>The output of the executed PowerShell script.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		/// <remarks>Be cautious with executing scripts received via API due to security risks.</remarks>
		public static string RunPowerShellScript(string script)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;
				process.Start();
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return output;
			}
		}

	}
}
