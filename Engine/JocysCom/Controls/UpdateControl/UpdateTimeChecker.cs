using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Class responsible for managing update checks based on timing settings.
	/// </summary>
	public class UpdateTimeChecker : NotifyPropertyChanged
	{
		public UpdateTimeChecker()
		{
			// Optionally initialize with default settings
			Settings = new UpdateTimeSettings();
		}

		private UpdateTimeSettings _Settings;
		/// <summary>
		/// The UpdateTimeSettings object that this checker uses.
		/// </summary>
		public UpdateTimeSettings Settings
		{
			get => _Settings;
			set
			{
				if (_Settings != null)
				{
					_Settings.PropertyChanged -= Settings_PropertyChanged;
				}
				_Settings = value;
				if (_Settings != null)
				{
					_Settings.PropertyChanged += Settings_PropertyChanged;
				}
				OnPropertyChanged();
				Start();
			}
		}

		private Timer _updateCheckTimer;
		private readonly object _timerLock = new object();

		[DefaultValue(null)]
		public string LastError { get => _LastError; set => SetProperty(ref _LastError, value); }
		private string _LastError;


		/// <summary>
		/// Starts the timer that periodically checks if it's time to check for updates.
		/// </summary>
		public void Start()
		{
			Stop();
			var settings = Settings;
			if (settings == null || !settings.IsEnabled)
				return;
			// Calculate interval until the next update
			var interval = settings.GetNextUpdate().Subtract(DateTime.Now);
			if (interval <= TimeSpan.Zero)
			{
				// If the interval is negative or zero, check immediately
				interval = TimeSpan.Zero;
			}
			lock (_timerLock)
			{
				// Initialize the timer to trigger once after the calculated interval
				_updateCheckTimer = new Timer(UpdateCheckTimer_Tick, null, interval, Timeout.InfiniteTimeSpan);
			}
		}

		/// <summary>
		/// Stops the update check timer.
		/// </summary>
		public void Stop()
		{
			lock (_timerLock)
			{
				_updateCheckTimer?.Dispose();
				_updateCheckTimer = null;
			}
		}

		private void UpdateCheckTimer_Tick(object state)
		{
			lock (_timerLock)
			{
				if (_updateCheckTimer == null)
					return;
				CheckForUpdatesAsync();
				// After checking for updates, reschedule the timer for the next update
				ScheduleNextCheck();
			}
		}

		private void ScheduleNextCheck()
		{
			if (_updateCheckTimer == null || Settings == null || !Settings.IsEnabled)
				return;

			// Calculate the interval until the next update
			var interval = Settings.GetNextUpdate().Subtract(DateTime.Now);
			if (interval <= TimeSpan.Zero)
			{
				// Prevent immediate rescheduling by setting a minimum interval.
				interval = TimeSpan.FromSeconds(1);
			}
			// Reschedule the timer to trigger once after the new interval
			_updateCheckTimer.Change(interval, Timeout.InfiniteTimeSpan);
		}

		/// <summary>
		/// Checks if it's time to check for updates and triggers the update process if necessary.
		/// </summary>
		private async void CheckForUpdatesAsync()
		{
			await Task.Delay(0); // If you have any asynchronous operations
			if (Settings?.IsEnabled == true && Settings.ShouldCheckForUpdates())
			{
				try
				{
					// Raise an event to notify listeners that it's time to update.
					UpdateRequired?.Invoke(this, EventArgs.Empty);
					LastError = null;
				}
				catch (Exception ex)
				{
					LastError = ex.Message;
				}
			}
			// Update the LastUpdate time.
			Settings.LastUpdate = DateTime.UtcNow;
		}

		private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// If the settings change, restart the timer to reflect new intervals.
			Start();
		}

		/// <summary>
		/// Event raised when it's time to check for updates.
		/// </summary>
		public event EventHandler UpdateRequired;
	}
}
