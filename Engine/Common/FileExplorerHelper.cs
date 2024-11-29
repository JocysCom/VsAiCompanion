using System;
using System.Diagnostics;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class FileExplorerHelper
	{
		/// <summary>
		/// Opens Windows Explorer and selects the specific file.
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void OpenFileInExplorerAndSelect(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty.");

			if (!System.IO.File.Exists(filePath))
				throw new ArgumentException("File does not exist.", nameof(filePath));

			var args = $"/select,\"{filePath}\"";
			Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
		}

		/// <summary>
		/// Opens the file using the default action in the Windows Explorer menu.
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void OpenFile(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty.");

			if (!System.IO.File.Exists(filePath))
				throw new ArgumentException("File does not exist.", nameof(filePath));

			Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
		}

		/// <summary>
		/// Edits the file using the associated program for editing (like the context menu in Windows Explorer).
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void EditFile(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty.");

			if (!System.IO.File.Exists(filePath))
				throw new ArgumentException("File does not exist.", nameof(filePath));

			var editVerb = "edit";
			var processStartInfo = new ProcessStartInfo
			{
				FileName = filePath,
				UseShellExecute = true,
				Verb = editVerb
			};

			try
			{
				Process.Start(processStartInfo);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Failed to edit the file. Ensure an associated editor is available.", ex);
			}
		}
	}
}
