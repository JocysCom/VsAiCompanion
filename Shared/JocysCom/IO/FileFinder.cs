using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.IO
{
	/// <summary>
	/// Traverses directories to find files matching patterns, optionally recursively, reporting structured progress (ProgressStatus) via <see cref="ProgressEventArgs"/> events.
	/// Extend filtering logic via <see cref="IsIgnored"/>.
	/// </summary>
	public class FileFinder
	{

		/// <summary>Occurs when a file is found or ignored during traversal, providing details in <see cref="ProgressEventArgs"/>.</summary>
		public event EventHandler<ProgressEventArgs> FileFound;

		// Tracks the index of the current directory in _Directories for progress reporting.
		int _DirectoryIndex;
		// The list of root directories being scanned.
		List<DirectoryInfo> _Directories;
		public bool IsPaused { get; set; }

		public bool IsStopping { get; set; }

		/// <summary>
		/// Retrieves files matching <paramref name="searchPattern"/> from specified <paramref name="paths"/>, optionally recursively.
		/// Raises <see cref="FileFound"/> events for each file and respects <see cref="IsPaused"/>/<see cref="IsStopping"/> flags.
		/// </summary>
		/// <param name="searchPattern">The search pattern(s), supports ';'-separated patterns.</param>
		/// <param name="allDirectories">True to include subdirectories in the search.</param>
		/// <param name="paths">One or more directory paths to search.</param>
		/// <returns>A list of <see cref="FileInfo"/> objects for the found files.</returns>
		public List<FileInfo> GetFiles(string searchPattern, bool allDirectories = false, params string[] paths)
		{
			IsStopping = false;
			IsPaused = false;
			var fis = new List<FileInfo>();
			_Directories = paths.Select(x => new DirectoryInfo(x)).ToList();
			for (int i = 0; i < _Directories.Count; i++)
			{
				// Pause or Stop.
				while (IsPaused && !IsStopping)
				{
					// Logical delay without blocking the current hardware thread.
					var resetEvent = new ManualResetEventSlim(false); _ = Task.Run(async () => await Task.Delay(500)); resetEvent.Wait();
				}
				if (IsStopping)
					return fis;
				// Do tasks.
				_DirectoryIndex = i;
				var di = _Directories[i];
				// Skip folders if don't exists.
				if (!di.Exists)
					continue;
				AddFiles(di.FullName, di, ref fis, searchPattern, allDirectories);
			}
			return fis;
		}

		/// <summary>
		/// Delegate to determine if a file or directory should be ignored during scanning.
		/// The first parameter is the original root directory path provided to <see cref="GetFiles"/>.
		/// The second parameter is the full path of the current file or directory.
		/// The third parameter is the size in bytes of the file (0 for directories).
		/// </summary>
		/// <returns>True to skip the item; otherwise, false.</returns>
		public Func<string, string, long, bool> IsIgnored;

		/// <summary>
		/// Adds files from <paramref name="di"/> to <paramref name="fileList"/> matching <paramref name="searchPattern"/>,
		/// raising <see cref="FileFound"/> events. Honors <paramref name="allDirectories"/> for recursion and <see cref="IsIgnored"/> filter.
		/// </summary>
		/// <param name="rootPath">Original root path for context in <see cref="IsIgnored"/>.</param>
		/// <param name="di">The directory to scan.</param>
		/// <param name="fileList">Reference to the list accumulating discovered files.</param>
		/// <param name="searchPattern">Pattern to match file names, supports ';'-separated values.</param>
		/// <param name="allDirectories">Whether to recurse into subdirectories.</param>
		public void AddFiles(string rootPath, DirectoryInfo di, ref List<FileInfo> fileList, string searchPattern, bool allDirectories)
		{
			try
			{
				// Skip system folder.
				//if (di.Name == "System Volume Information")
				//    return;
				var patterns = searchPattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (patterns.Length == 0)
				{
					// Lookup for all files.
					patterns = new[] { "" };
				}
				for (int p = 0; p < patterns.Length; p++)
				{
					var pattern = patterns[p];
					var files = string.IsNullOrEmpty(pattern)
						? di.GetFiles()
						: di.GetFiles(pattern);
					for (int i = 0; i < files.Length; i++)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
						{
							// Logical delay without blocking the current hardware thread.
							var resetEvent = new ManualResetEventSlim(false); _ = Task.Run(async () => await Task.Delay(500)); resetEvent.Wait();
						}
						if (IsStopping)
							return;
						// Do tasks.
						var fullName = files[i].FullName;
						if (!fileList.Any(x => x.FullName == fullName))
						{
							var isIgnored = IsIgnored?.Invoke(rootPath, fullName, files[i].Length) == true;
							if (!isIgnored)
							{
								fileList.Add(files[i]);
							}
							var ev = FileFound;
							if (ev is null)
								continue;
							// Report progress.
							var e = new ProgressEventArgs();
							e.TopIndex = _DirectoryIndex;
							e.TopCount = _Directories.Count;
							e.TopData = _Directories;
							e.SubIndex = fileList.Count - 1;
							e.SubCount = 0;
							e.SubData = fileList;
							e.State = isIgnored ? ProgressStatus.Ignored : ProgressStatus.Updated;
							e.TopMessage = $"Scan Folder: {_Directories[(int)e.TopIndex].FullName}";
							var file = fileList[(int)e.SubIndex];
							var name = file.FullName;
							var size = BytesToString(file.Length);
							e.SubMessage = $"File: {name} ({size})";
							ev(this, e);
						}
					}
				}
			}
			catch (Exception ex)
			{
				var _ = ex.ToString();
			}
			try
			{
				// If must search inside subdirectories then...
				if (allDirectories)
				{
					var subDis = di.GetDirectories();
					foreach (DirectoryInfo subDi in subDis)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
						{
							// Logical delay without blocking the current hardware thread.
							var resetEvent = new ManualResetEventSlim(false); _ = Task.Run(async () => await Task.Delay(500)); resetEvent.Wait();
						}
						if (IsStopping)
							return;
						if (IsIgnored?.Invoke(rootPath, subDi.FullName, 0) == true)
							continue;
						// Do tasks.
						AddFiles(rootPath, subDi, ref fileList, searchPattern, allDirectories);
					}
				}
			}
			catch (Exception ex)
			{
				var _ = ex.ToString();
			}

		}

		/// <summary>
		/// Formats a numeric value into a human-readable string with SI unit suffixes by computing the logarithm index.
		/// </summary>
		/// <param name="value">The numeric value to format.</param>
		/// <param name="format">The composite format string.</param>
		/// <param name="newBase">The base used for unit conversion (e.g., 1000 for SI, 1024 for binary).</param>
		/// <returns>A formatted string with unit suffix.</returns>
		static string SizeToString(long value, string format = "{0:0.##} {1}", int newBase = 1000)
		{
			// Suffixes: Kilo, Mega, Giga, Tera, Peta, Exa.
			string[] suffix = { "", "K", "M", "G", "T", "P", "E" };
			var absolute = Math.Abs(value);
			if (value == 0)
				return string.Format(format, value, suffix[0]);
			var index = (int)Math.Floor(Math.Log(absolute, newBase));
			var number = Math.Round(absolute / Math.Pow(newBase, index), 1);
			var signed = Math.Sign(value) * number;
			return string.Format(format, signed, suffix[index]);
		}

		/// <summary>Converts a byte count to a human-readable string with binary (1024) unit suffixes.</summary>
		/// <param name="value">The number of bytes.</param>
		/// <returns>A formatted string, e.g. "1,024 KB".</returns>
		public static string BytesToString(long value)
			=> SizeToString(value, "{0:#,##0} {1}B", 1024);

	}
}
