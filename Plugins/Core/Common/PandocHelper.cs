using System;
using System.Diagnostics;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// File converter.
	/// </summary>
	public class PandocHelper
	{

		/// <summary>
		/// Path to folder where pandoc must be installed.
		/// </summary>
		public static string ToolFolder { get; set; }


		/// <summary>
		/// Convert files.
		/// </summary>
		public static string ConvertDocument(string inputFilePath, string outputFilePath)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "pandoc",
				Arguments = $"{inputFilePath} -o {outputFilePath}",
				RedirectStandardOutput = true
			};

			using (var process = Process.Start(startInfo))
			{
				process.WaitForExit();
				if (process.ExitCode == 0)
				{
					// Read the output Markdown content, if necessary
					string markdownContent = System.IO.File.ReadAllText(outputFilePath);
					return markdownContent;
				}
				else
				{
					throw new Exception("Failed to convert document to Markdown.");
				}
			}
		}

	}
}
