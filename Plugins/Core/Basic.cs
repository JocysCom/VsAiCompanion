using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Helps AI to auto-continue on the task.
	/// </summary>
	public class Basic
	{

		/// <summary>
		/// Use when you can't provide an answer in one response and need to split the answer.
		/// Use after reply when user asks to generate answers with permission to continue.
		/// Continue with the next part of the reply after this function call return "Please continue".
		/// </summary>
		/// <returns>The output message to reply from the user.</returns>
		/// <param name="reserved">Reserved. Send empty string as a value.</param>
		/// <exception cref="System.Exception">Error message explaining why execution failed.</exception>
		public static string AutoContinue(string reserved)
		{
			return "Please continue.";
		}

		/// <summary>
		/// Use to retrieve content of websites by URL.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		public static async Task<string> GetWebPageContents(string url)
		{
			using (var client = new HttpClient())
			{
				try
				{
					var response = await client.GetAsync(url);
					if (response.IsSuccessStatusCode)
					{
						string content = await response.Content.ReadAsStringAsync();
						return content;
					}
					return $"Error: Unable to fetch the page. Status Code: {response.StatusCode}";
				}
				catch (Exception ex)
				{
					return $"Error: {ex.Message}";
				}
			}
		}

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
