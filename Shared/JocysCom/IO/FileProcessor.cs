using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.IO
{
	/// <summary>
	/// Orchestrates batch scanning and processing of files with progress reporting.
	/// </summary>
	/// <remarks>
	/// Uses FileFinder for file enumeration and subscribes to its FileFound events.
	/// Exposes a <see cref="ProcessItem"/> delegate for external processing logic.
	/// </remarks>
	public class FileProcessor
	{

		public FileProcessor()
		{
			FileFinder = new FileFinder();
			FileFinder.FileFound += ff_FileFound;
			Cancellation = new CancellationTokenSource();
		}

		private void ff_FileFound(object sender, ProgressEventArgs e)
			=> Report(e);

		#region ■ IProgress

		/// <summary>
		/// Raised when progress is reported during scanning and processing.
		/// </summary>
		public event EventHandler<ProgressEventArgs> Progress;

		/// <summary>
		/// Delegate invoked for each file to perform processing; returns a ProgressStatus indicating the result.
		/// </summary>
		public Func<FileProcessor, ProgressEventArgs, Task<ProgressStatus>> ProcessItem;

		public void Report(ProgressEventArgs e)
			=> Progress?.Invoke(this, e);

		#endregion

		/// <summary>
		/// CancellationTokenSource used to request cancellation of the current scan operation.
		/// </summary>
		public CancellationTokenSource Cancellation;

		public DateTime DateStarted => _DateStarted;
		private DateTime _DateStarted;
		public DateTime DateEnded => _DateEnded;
		private DateTime _DateEnded;

		/// <summary>
		/// Tracks counts of processed items by their ProgressStatus.
		/// </summary>
		public Dictionary<ProgressStatus, int> ProcessItemStates =
			Enum.GetValues(typeof(ProgressStatus))
				.Cast<ProgressStatus>()
				.ToDictionary(x => x, x => 0);

		public bool IsStopping { get => FileFinder.IsStopping; set => FileFinder.IsStopping = value; }

		public bool IsPaused { get => FileFinder.IsPaused; set => FileFinder.IsPaused = value; }

		public readonly FileFinder FileFinder;

		/// <summary>
		/// Scans the specified directories for files matching <paramref name="searchPattern"/>,
		/// processes each file via <see cref="ProcessItem"/>, and raises <see cref="Progress"/> events.
		/// </summary>
		/// <param name="paths">Directories to scan; Windows system folders are excluded.</param>
		/// <param name="searchPattern">Optional pattern for file search (e.g., "*.txt").</param>
		/// <param name="allDirectories">True to include all subdirectories; otherwise only top-level.</param>
		/// <remarks>
		/// Initializes DateStarted, resets ProcessItemStates, and reports Started/Completed states.
		/// </remarks>
		public async Task Scan(string[] paths, string searchPattern = null, bool allDirectories = false)
		{
			_DateStarted = DateTime.Now;
			IsStopping = false;
			IsPaused = false;
			var keys = ProcessItemStates.Keys.ToArray();
			foreach (var key in keys)
				ProcessItemStates[key] = 0;
			// Step 1: Get list of files inside the folder.
			var e = new ProgressEventArgs
			{
				State = ProgressStatus.Started
			};
			Report(e);
			var winFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			var dirs = paths
				.Select(x => x)
				// Except win folders.
				.Where(x => !x.StartsWith(winFolder, StringComparison.OrdinalIgnoreCase))
				.ToArray();
			// Create list to store file to scan.
			var files = FileFinder.GetFiles(searchPattern, allDirectories, dirs);
			// Step 2: Scan files.
			var topMessage = "Process Files.";
			var topMessageStates = "";
			for (var i = 0; i < files.Count; i++)
			{
				if (Cancellation.IsCancellationRequested)
					break;
				var file = files[i];
				e = new ProgressEventArgs
				{
					TopMessage = topMessage + topMessageStates,
					TopIndex = i,
					TopCount = files.Count,
					TopData = files,
					SubIndex = 0,
					SubCount = 0,
				};
				var size = FileFinder.BytesToString(file.Length);
				var name = file.FullName;
				e.SubMessage = $"File: {name} ({size})";
				// Get info by full name.
				e.SubData = file;
				e.State = ProgressStatus.Processing;
				// Process the file and return procesing state.
				e.ProcessItemState = await ProcessItem(this, e);
				Report(e);
				e.State = ProgressStatus.Updated;
				Report(e);
				ProcessItemStates[e.ProcessItemState] = ProcessItemStates[e.ProcessItemState] + 1;
				var processingStateMessages = ProcessItemStates.Where(kv => kv.Value > 0)
					.Select(kv => $" {kv.Key} = {kv.Value}.");
				topMessageStates = string.Join(" ", processingStateMessages);
				e.TopMessage = topMessage + topMessageStates;
				Report(e);
			}
			_DateEnded = DateTime.Now;
			e = new ProgressEventArgs
			{
				State = ProgressStatus.Completed
			};
			Report(e);
		}

		/// <summary>
		/// Constructs a summary message listing counts per ProgressStatus and total count.
		/// </summary>
		/// <returns>A multi-line string with individual and total counts.</returns>
		public string GetProcessCompletedMessage()
		{
			var states =
				ProcessItemStates
					.Where(x => x.Value > 0)
					.Select(x => $"{x.Key}: {x.Value}")
					.ToList();
			var totalCount = ProcessItemStates.Sum(x => x.Value);
			states.Add($"TOTAL: {totalCount}");
			var logMessage = string.Join("\r\n", states);
			var message = $"\r\nProcess Completed\r\n{logMessage}\r\n";
			return message;
		}
	}
}