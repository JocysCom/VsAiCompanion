using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class FileExplorerHelper
	{
		private static bool IsValidFilePath(string path)
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
			if (!IsValidFilePath(filePath))
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
			if (!IsValidFilePath(filePath))
				return;
			Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
		}

		/// <summary>
		/// Edits the file using the associated program for editing (like the context menu in Windows Explorer).
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void EditFile(string filePath)
		{
			if (!IsValidFilePath(filePath))
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

		/// <summary>
		/// Opens the "Open With..." dialog for the specified file using SHOpenWithDialog.
		/// </summary>
		/// <param name="filePath">The full path to the file.</param>
		public static void OpenWithFile(string filePath)
		{
			if (!IsValidFilePath(filePath))
				return;

			OPENASINFO oainfo = new OPENASINFO
			{
				cszFile = filePath,
				cszClass = null,
				OpenAsInfoFlags = OpenAsInfoFlags.OAIF_FORCE_REGISTRATION | OpenAsInfoFlags.OAIF_EXEC
			};
			int result = SHOpenWithDialog(IntPtr.Zero, ref oainfo);
			if (result != 0)
			{
				// Handle error
				Global.MainControl.InfoPanel.SetBodyError("Failed to open 'Open With' dialog.");
			}
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO oainfo);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct OPENASINFO
		{
			[MarshalAs(UnmanagedType.LPWStr)]
			public string cszFile;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string cszClass;
			public OpenAsInfoFlags OpenAsInfoFlags;
		}

		[Flags]
		private enum OpenAsInfoFlags : uint
		{
			OAIF_ALLOW_REGISTRATION = 0x00000001,
			OAIF_REGISTER_EXT = 0x00000002,
			OAIF_EXEC = 0x00000004,
			OAIF_FORCE_REGISTRATION = 0x00000008,
			OAIF_HIDE_REGISTRATION = 0x00000020,
			OAIF_URL_PROTOCOL = 0x00000040,
			OAIF_FILE_IS_URI = 0x00000080
		}

	}
}
