using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.IO
{
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

		public event EventHandler<ProgressEventArgs> Progress;

		public Func<FileProcessor, ProgressEventArgs, Task<ProgressStatus>> ProcessItem;

		public void Report(ProgressEventArgs e)
			=> Progress?.Invoke(this, e);

		#endregion

		public CancellationTokenSource Cancellation;

		public DateTime DateStarted => _DateStarted;
		private DateTime _DateStarted;
		public DateTime DateEnded => _DateEnded;
		private DateTime _DateEnded;

		public Dictionary<ProgressStatus, int> ProcessItemStates =
			Enum.GetValues(typeof(ProgressStatus))
				.Cast<ProgressStatus>()
				.ToDictionary(x => x, x => 0);

		public bool IsStopping { get => FileFinder.IsStopping; set => FileFinder.IsStopping = value; }

		public bool IsPaused { get => FileFinder.IsPaused; set => FileFinder.IsPaused = value; }

		public readonly FileFinder FileFinder;

		public async Task Scan(string[] paths, string searchPattern = null, bool allDirectories = false)
		{
			_DateStarted = DateTime.Now;
			IsStopping = false;
			IsPaused = false;
			foreach (var key in ProcessItemStates.Keys)
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

	}
}
