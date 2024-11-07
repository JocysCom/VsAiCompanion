using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Class responsible for managing update checks based on timing settings.
	/// </summary>
	public class UpdateTimeChecker : INotifyPropertyChanged
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
				RestartUpdateCheckTimer();
			}
		}

		private Timer _updateCheckTimer;

		/// <summary>
		/// Starts the timer that periodically checks if it's time to check for updates.
		/// </summary>
		public void Start()
		{
			StartUpdateCheckTimer();
		}

		/// <summary>
		/// Stops the update check timer.
		/// </summary>
		public void Stop()
		{
			StopUpdateCheckTimer();
		}

		private void StartUpdateCheckTimer()
		{
			StopUpdateCheckTimer();
			if (Settings != null && Settings.IsEnabled)
			{
				var interval = GetTimerInterval();
				_updateCheckTimer = new Timer(UpdateCheckTimer_Tick, null, TimeSpan.Zero, interval);
			}
		}

		private void RestartUpdateCheckTimer()
		{
			StartUpdateCheckTimer();
		}

		private void StopUpdateCheckTimer()
		{
			if (_updateCheckTimer != null)
			{
				_updateCheckTimer.Dispose();
				_updateCheckTimer = null;
			}
		}

		private TimeSpan GetTimerInterval()
		{
			// Calculate interval based on settings
			// For this example, check every hour. Adjust as needed.
			return TimeSpan.FromHours(1);
		}

		private void UpdateCheckTimer_Tick(object state)
		{
			CheckForUpdatesAsync();
		}

		/// <summary>
		/// Checks if it's time to check for updates and triggers the update process if necessary.
		/// </summary>
		private async void CheckForUpdatesAsync()
		{
			await Task.Delay(0); // If you have any asynchronous operations
			if (Settings?.IsEnabled == true && Settings.ShouldCheckForUpdates())
			{
				// Raise an event to notify listeners that it's time to update.
				UpdateRequired?.Invoke(this, EventArgs.Empty);

				// Update the LastUpdate time.
				Settings.LastUpdate = DateTime.UtcNow;
			}
		}

		private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// If the settings change, restart the timer to reflect new intervals.
			RestartUpdateCheckTimer();
		}

		/// <summary>
		/// Event raised when it's time to check for updates.
		/// </summary>
		public event EventHandler UpdateRequired;

		#region INotifyPropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		#endregion
	}
}
