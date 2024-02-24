using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using JocysCom.VS.AiCompanion.Shared;
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
		/// <returns>Automatic reply from the user.</returns>
		/// <param name="reserved">Reserved. Send empty string as a value.</param>
		/// <exception cref="System.Exception">Error message explaining why execution failed.</exception>
		[RiskLevel(RiskLevel.Low)]
		public static string AutoContinue(string reserved)
		{
			return "Please continue.";
		}

		/// <summary>
		/// Retrieve content of websites by URL.
		/// </summary>
		/// <param name="url">URL which points to the resource.</param>
		/// <returns>The output of the request.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		[RiskLevel(RiskLevel.Low)]
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
		/// Execute a process on user computer. Use `System.Diagnostics.ProcessStartInfo`
		/// </summary>
		/// <param name="fileName">Application or document to start.</param>
		/// <param name="arguments">Command-line arguments to use when starting the application.</param>
		/// <param name="workingDirectory">The working directory for the process to be started. Default is empty string.</param>
		/// <returns>The output of the started process.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		/// <remarks>Be cautious with executing process requesys received via API due to security risks.</remarks>
		[RiskLevel(RiskLevel.High)]
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
		/// Execute a PowerShell script on user computer.
		/// </summary>
		/// <param name="script">The PowerShell script to be executed.</param>
		/// <returns>The output of the executed PowerShell script.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		/// <remarks>Be cautious with executing scripts received via API due to security risks.</remarks>
		[RiskLevel(RiskLevel.High)]
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

		#region File Operations

		/// <summary>
		/// Read file information and contents.
		/// </summary>
		/// <param name="path">The file to read from.</param>
		[RiskLevel(RiskLevel.Medium)]
		public static DocItem ReadFile(string path)
		{
			var di = new DocItem(null, path);
			di.LoadData();
			return di;
		}

		/// <summary>
		/// Write file text content on user computer.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="contents">The string to write to the file.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		public static bool WriteFile(string path, string contents)
		{
			System.IO.File.WriteAllText(path, contents);
			return true;
		}

		/// <summary>
		/// Compares two files and returns a textual representation of the changes.
		/// </summary>
		/// <param name="originalFilePath">The path to the original file.</param>
		/// <param name="modifiedFilePath">The path to the modified file.</param>
		/// <returns>A string detailing the changes made from the original file to the modified file.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public static string CompareFilesAndReturnChanges(string originalFilePath, string modifiedFilePath)
			=> DiffHelper.CompareFilesAndReturnChanges(originalFilePath, modifiedFilePath);

		/// <summary>
		/// Updates content of the current document by applying a series of changes in the unified diff format as recognized by the diff-match-patch library. 
		/// This format is focused on efficient text manipulation, supporting insertions, deletions, and modifications,
		/// especially useful when bandwidth or storage is limited.
		/// </summary>
		/// <example>
		/// 'changes' value Example:
		/// @@ -14,12 +14,9 @@
		///  rld 
		/// -1%0ARemove
		/// +2%0AAdd
		/// </example>
		/// <param name="filePath">The path to the file that needs to be updated.</param>
		/// <param name="changes">The string representation of the changes to apply, adhering to the diff format specified above.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		[RiskLevel(RiskLevel.High)]
		public static string ApplyFileChanges(string filePath, string changes)
			=> DiffHelper.PatchFile(filePath, changes);

		#endregion

	}

}
