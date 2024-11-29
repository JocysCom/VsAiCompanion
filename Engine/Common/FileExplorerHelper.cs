using System;
using System.Diagnostics;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class FileExplorerHelper
	{
		private static bool IsValidFIlePath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				Global.MainControl.InfoPanel.SetBodyError("File path cannot be null or empty.");
				return false;
			}
			if (!System.IO.File.Exists(path))
			{
				Global.MainControl.InfoPanel.SetBodyError($"File '{path}' does not exist.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Opens Windows Explorer and selects the specific file.
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void OpenFileInExplorerAndSelect(string filePath)
		{
			if (!IsValidFIlePath(filePath))
				return;
			var args = $"/select,\"{filePath}\"";
			Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
		}

		/// <summary>
		/// Opens the file using the default action in the Windows Explorer menu.
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void OpenFile(string filePath)
		{
			if (!IsValidFIlePath(filePath))
				return;
			Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
		}

		/// <summary>
		/// Edits the file using the associated program for editing (like the context menu in Windows Explorer).
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void EditFile(string filePath)
		{
			if (!IsValidFIlePath(filePath))
				return;
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
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
		}
	}
}
