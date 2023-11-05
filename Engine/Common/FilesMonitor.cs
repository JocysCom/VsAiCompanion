using System;
using System.ComponentModel;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine
{
	internal class FilesMonitor
	{

		private FileSystemWatcher _folderWatcher;
		private System.Timers.Timer _debounceTimer;
		public event EventHandler FilesChanged;

		[DefaultValue(false)]
		public bool IsFolderMonitored { get; set; }

		public void SetFileMonitoring(bool enabled, string folderPath = null, string filePattern = null)
		{
			IsFolderMonitored = enabled;

			if (enabled)
			{
				if (_folderWatcher != null)
				{
					_folderWatcher.EnableRaisingEvents = false;
					_folderWatcher.Dispose();
				}

				_folderWatcher = new FileSystemWatcher(folderPath, filePattern)
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
				};

				_folderWatcher.Changed += OnChanged;
				_folderWatcher.Created += OnChanged;
				_folderWatcher.Deleted += OnChanged;
				_folderWatcher.Renamed += OnRenamed;

				_folderWatcher.EnableRaisingEvents = true;

				// Initialize the debounce timer with an interval of 500ms
				_debounceTimer = new System.Timers.Timer(500) { AutoReset = false };
				_debounceTimer.Elapsed += _debounceTimer_Elapsed;
			}
			else
			{
				if (_folderWatcher != null)
				{
					_folderWatcher.EnableRaisingEvents = false;
					_folderWatcher.Dispose();
					_folderWatcher = null;
				}

				if (_debounceTimer != null)
				{
					_debounceTimer.Stop();
					_debounceTimer.Dispose();
					_debounceTimer = null;
				}
			}
		}

		private void _debounceTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			FilesChanged?.Invoke(this, EventArgs.Empty);
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			DebounceEvent();
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			DebounceEvent();
		}

		private void DebounceEvent()
		{
			// Reset and start the debounce timer each time a file system event occurs
			_debounceTimer.Stop();
			_debounceTimer.Start();
		}

	}
}
