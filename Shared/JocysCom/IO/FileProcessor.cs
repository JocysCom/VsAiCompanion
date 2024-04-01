using System;
using System.Linq;

namespace JocysCom.ClassLibrary.IO
{
	public class FileProcessor
	{

		public FileProcessor()
		{
			ff = new FileFinder();
			ff.FileFound += ff_FileFound;
		}

		private void ff_FileFound(object sender, ProgressEventArgs e)
			=> Report(e);


		#region ■ IProgress

		public event EventHandler<ProgressEventArgs> Progress;

		public void Report(ProgressEventArgs e)
			=> Progress?.Invoke(this, e);

		#endregion

		public DateTime DateStarted => _DateStarted;
		private DateTime _DateStarted;
		public DateTime DateEnded => _DateEnded;
		private DateTime _DateEnded;

		public bool IsStopping { get => ff.IsStopping; set => ff.IsStopping = value; }

		public bool IsPaused { get => ff.IsPaused; set => ff.IsPaused = value; }

		private readonly FileFinder ff;

		public void Scan(string[] paths, string searchPattern = null)
		{
			_DateStarted = DateTime.Now;
			IsStopping = false;
			IsPaused = false;
			// Step 1: Get list of files inside the folder.
			var e = new ProgressEventArgs
			{
				State = ProgressStatus.Started
			};
			Report(e);
			var created = 0;
			var updated = 0;
			var winFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			var dirs = paths
				.Select(x => x)
				// Except win folders.
				.Where(x => !x.StartsWith(winFolder, StringComparison.OrdinalIgnoreCase))
				.ToArray();
			// Create list to store file to scan.
			var files = ff.GetFiles(searchPattern, false, dirs);
			// Step 2: Scan files.
			for (var i = 0; i < files.Count; i++)
			{
				var file = files[i];
				var topMessage = "Process Files.";
				if (created > 0)
					topMessage += $" Created = {created}.";
				if (updated > 0)
					topMessage += $" Updated = {updated}.";
				e = new ProgressEventArgs
				{
					TopMessage = topMessage,
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
				// At this point Progress event listened must process the file from subData and set state.
				Report(e);
				if (e.State == ProgressStatus.Created)
					created++;
				else if (e.State == ProgressStatus.Updated)
					updated++;
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
