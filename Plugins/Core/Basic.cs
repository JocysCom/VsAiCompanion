using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Basic functions that allow AI to create and modify files.
	/// </summary>
	public partial class Basic : IFileHelper, IDiffHelper
	{

		#region AI Tokens

		/// <summary>
		/// Get tokens.
		/// </summary>
		/// <param name="text">Input text.</param>
		/// <param name="count">Token count.</param>
		/// <param name="tokens">Token words.</param>
		/// <param name="modelName">Optiona. Model name. Default: "gpt-4o".</param>
		/// <exception cref="Exception"></exception>
		public static void GetTokens(string text, out int count, ref List<string> tokens, string modelName = null)
		{
			var model = string.IsNullOrEmpty(modelName)
				? "gpt-4o"
				: modelName;
			var encoder = Tiktoken.ModelToEncoder.TryFor(model);
			if (encoder == null)
			{
				var encoding = Tiktoken.ModelToEncoding.TryFor(model);
				if (encoding != null)
					encoder = new Tiktoken.Encoder(encoding);
			}
			if (encoder == null)
				throw new Exception($"Model name {modelName} not found!");
			count = encoder.CountTokens(text);
			if (tokens != null)
				// "hello world" => ["hello", " world"]
				tokens.AddRange(encoder.Explore(text));
			//var tokens = encoder.Encode("hello world"); // [15339, 1917]
			//var text = encoder.Decode(tokens); // hello world
		}

		/// <summary>
		/// Counts tokens. Does not take into account special tokens.
		/// </summary>
		/// <param name="text">Input text.</param>
		/// <param name="modelName">Open AI model name. Default: "gpt-4o".</param>
		[RiskLevel(RiskLevel.None)]
		public static OperationResult<int> CountTokens(string text, string modelName = null)
		{
			int count;
			List<string> tokens = null;
			try
			{
				GetTokens(text, out count, ref tokens, modelName);
				return new OperationResult<int>(count);
			}
			catch (Exception ex)
			{
				return new OperationResult<int>(ex);
			}
		}

		/// <summary>
		/// Counts the tokens in the specified files, excluding special tokens. 
		/// Returns an array of token counts, where each count position corresponds to the file's position in the `paths` array.
		/// </summary>
		/// <param name="paths">List of file paths to read from.</param>
		/// <param name="modelName">OpenAI model name (default: "gpt-4o").</param>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<int[]> CountFileTokens(string[] paths, string modelName = null)
		{
			var counts = new int[paths.Length];
			for (int i = 0; i < paths.Length; i++)
			{
				try
				{

					var result = fileHelper.ReadFileAsPlainText(paths[i]);
					if (!result.Success)
						result.ToResult<int[]>(null);
					//var text = System.IO.File.ReadAllText(paths[i]);
					counts[i] = CountTokens(result.Data, modelName).Data;
				}
				catch (Exception ex)
				{
					return new OperationResult<int[]>(ex);
				}
			}
			return new OperationResult<int[]>(counts);
		}

		#endregion

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
		/// Wait for a specified amount of time.
		/// </summary>
		/// <param name="millisecondsDelay">The number of milliseconds to wait. -1 to wait indefinitely.</param>
		[RiskLevel(RiskLevel.None)]
		public static async Task<OperationResult<bool>> Wait(int millisecondsDelay)
		{
			await Task.Delay(millisecondsDelay);
			return new OperationResult<bool>(true);
		}

		/// <summary>
		/// Get the current system information: Current Date, OS Version, Architecture, Locale, Time Zone and GPS Geo Location.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public async static Task<List<KeyValue>> GetCurrentSystemInfo()
		{
			var pos = await GpsLocation.GetCurrentLocation();

			var list = new List<KeyValue>() {
				new KeyValue("Current Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
				new KeyValue("OS Version", Environment.OSVersion.VersionString),
				new KeyValue("OS Architecture", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
				new KeyValue("Locale", CultureInfo.CurrentCulture.Name),
				new KeyValue("Time Zone", TimeZoneInfo.Local.DisplayName),
				new KeyValue("GPS Altitude", $"{pos.altitude}"),
				new KeyValue("GPS Latitude", $"{pos.latitude}"),
				new KeyValue("GPS Longitude", $"{pos.longitude}"),
			};
			return list;
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
		[RiskLevel(RiskLevel.Critical)]
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
		[RiskLevel(RiskLevel.Critical)]
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
