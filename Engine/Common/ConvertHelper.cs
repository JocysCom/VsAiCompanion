using JocysCom.ClassLibrary.Processes;
using System.Diagnostics;
using System.Text;

namespace JocysCom.VS.AiCompanion.Engine
{
	internal class ConvertHelper
	{
		/// <summary>
		/// Convert data files.
		/// </summary>
		/// <param name="scriptPath">Full path to ConvertModelTraininData.ps1</param>
		public static string ConvertModelTrainingData(
			string scriptPath,
			string conversionType,
			string fineTuningName,
			string dataFolderName,
			string sourceFileName,
			string targetFileName,
			string systemPromptContent
		)
		{
			var arguments = $"-ConversionType \"{conversionType}\" -FineTuningName \"{fineTuningName}\" -DataFolderName \"{dataFolderName}\" -SourceFileName \"{sourceFileName}\" -TargetFileName \"{targetFileName}\" -SystemPromptContent \"{systemPromptContent}\"";
			using (var shell = new HiddenShell())
				return shell.ExecuteCommand($"PowerShell -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}");

			/*
			var processInfo = new ProcessStartInfo();
			processInfo.FileName = "PowerShell.exe";
			processInfo.RedirectStandardOutput = true;
			processInfo.RedirectStandardError = true;
			processInfo.UseShellExecute = false;
			processInfo.CreateNoWindow = true;
			processInfo.Arguments = $"-ExecutionPolicy ByPass -File \"{scriptPath}\" " +
				$"-ConversionType \"{conversionType}\" " +
				$"-FineTuningName \"{fineTuningName}\" " +
				$"-DataFolderName \"{dataFolderName}\" " +
				$"-SourceFileName \"{sourceFileName}\" " +
				$"-TargetFileName \"{targetFileName}\" " +
				$"-SystemPromptContent \"{systemPromptContent}\"";
			var process = new Process();
			process.StartInfo = processInfo;
			var output = new StringBuilder();
			var errors = new StringBuilder();
			process.OutputDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
					output.AppendLine(e.Data);
			};
			process.ErrorDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
					errors.AppendLine(e.Data);
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
			string outputString = output.ToString();
			string errorsString = errors.ToString();
			return outputString + errorsString;
			*/
		}
	}
}
