using System.IO;
using System.Windows.Forms;

namespace JocysCom.ClassLibrary.Controls
{

	public class DialogHelper
	{

		#region Open File Dialog

		/// <summary>
		/// Update dialog.
		/// </summary>
		/// <param name="dialog">Dialog to fix.</param>
		/// <param name="defaultInitialDirectory">Can be application root path.</param>
		public static void FixDialogFile(FileDialog dialog, string defaultInitialDirectory = null)
		{
			// c:\Users\Public\Desktop
			var dir = dialog.InitialDirectory;
			// Process Directory.
			var dirInfo = string.IsNullOrEmpty(dir)
				? new DirectoryInfo(defaultInitialDirectory)
				: new DirectoryInfo(dir);
			var newInfo = dirInfo;
			// If root folder exist then continue...
			if (dirInfo.Root.Exists)
			{
				bool DirExist = false;
				while (!DirExist)
				{
					if (newInfo.Exists) DirExist = true;
					else newInfo = newInfo.Parent;
				}
			}
			// Set updated initial directory.
			dialog.InitialDirectory = newInfo.FullName;
		}

		public static void AddFilter(FileDialog dialog, string extension = ".*", string description = null)
		{
			if (extension == ".*")
			{
				if (string.IsNullOrEmpty(description))
					description = "All files";
			}
			else
			{
				if (string.IsNullOrEmpty(description))
					description = JocysCom.ClassLibrary.Files.Mime.GetFileDescription(extension);
			}
			var filter = string.Format("{0} (*{1})|*{1}", description, extension);
			if (string.IsNullOrEmpty(dialog.Filter))
				dialog.Filter = filter;
			else
				dialog.Filter += "|" + filter;
		}

		#endregion

		#region Open Folder Dialog

		/// <summary>
		/// Update dialog.
		/// </summary>
		/// <param name="dialog">Dialog to fix.</param>
		/// <param name="defaultInitialDirectory">Can be application root path.</param>
		public static void FixDialogFolder(FolderBrowserDialog dialog, string defaultInitialDirectory = null)
		{
			// If given path is not rooted (= relative).
			if (!Path.IsPathRooted(dialog.SelectedPath))
			{
				// Combine application path and rooted path;
				string newPath = Path.Combine(defaultInitialDirectory, dialog.SelectedPath);
				dialog.SelectedPath = newPath;
			}
			// Set default path to ApplicationRoot.
			var dirInfo = new DirectoryInfo(defaultInitialDirectory);
			// Try to convert given path to Directory Info.
			try
			{
				dirInfo = new DirectoryInfo(dialog.SelectedPath);
			}
			catch
			{
				//tbxLog.AppendText("Open folder:" + dirInfo.FullName);
			}
			// Create new Directory info.
			var newInfo = dirInfo;
			// If root folder exist then continue...
			if (newInfo.Root.Exists)
			{
				bool DirExist = false;
				while (!DirExist)
				{
					if (newInfo.Exists) DirExist = true;
					else newInfo = newInfo.Parent;
				}
			}
			else
			{
				// Reset path to ApplicationRoot.
				newInfo = new DirectoryInfo(defaultInitialDirectory);
			}
			//MessageBox.Show(newInfo.FullName);
			dialog.SelectedPath = newInfo.FullName;
		}

		#endregion

	}
}
