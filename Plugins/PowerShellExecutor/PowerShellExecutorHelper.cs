using System.Diagnostics;

namespace JocysCom.VS.AiCompanion.Plugins.PowerShellExecutor
{
	public class PowerShellExecutorHelper
	{

		/// <summary>
		/// Execute a PowerShell script
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
