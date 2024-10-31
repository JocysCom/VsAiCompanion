using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// User control for configuring update time settings.
	/// </summary>
	public partial class UpdateTimeUserControl : UserControl, INotifyPropertyChanged
	{
		public UpdateTimeUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;

			// Start the update check timer
			StartUpdateCheckTimer();
		}

		/// <summary>
		/// The UpdateTimeSettings object that this control binds to.
		/// </summary>
		public UpdateTimeSettings Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				_Item = value;
				if (_Item != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				OnPropertyChanged(nameof(Item));

				// Check for updates on app start if enabled
				if (_Item?.CheckOnAppStart == true)
				{
					CheckForUpdatesAsync();
				}
			}
		}
		UpdateTimeSettings _Item;

		/// <summary>
		/// Provides a dictionary of time units for use in the frequency unit ComboBox.
		/// </summary>
		public Dictionary<TimeUnitType, string> TimeUnitTypes =>
			JocysCom.ClassLibrary.Runtime.Attributes.GetDictionary(new[] {
				TimeUnitType.Hour, TimeUnitType.Day, TimeUnitType.Week, TimeUnitType.Month
			});

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
		}

		private DispatcherTimer _updateCheckTimer;

		/// <summary>
		/// Starts a timer that periodically checks if it's time to check for updates.
		/// </summary>
		private void StartUpdateCheckTimer()
		{
			// Initialize the timer with an interval appropriate for your application, e.g., 1 hour.
			_updateCheckTimer = new DispatcherTimer();
			_updateCheckTimer.Interval = TimeSpan.FromMinutes(30); // or any other interval
			_updateCheckTimer.Tick += UpdateCheckTimer_Tick;
			_updateCheckTimer.Start();
		}

		/// <summary>
		/// Stops the update check timer.
		/// </summary>
		private void StopUpdateCheckTimer()
		{
			if (_updateCheckTimer != null)
			{
				_updateCheckTimer.Stop();
				_updateCheckTimer.Tick -= UpdateCheckTimer_Tick;
				_updateCheckTimer = null;
			}
		}

		private void UpdateCheckTimer_Tick(object sender, EventArgs e)
		{
			CheckForUpdatesAsync();
		}

		/// <summary>
		/// Checks if it's time to check for updates and triggers the update process if necessary.
		/// </summary>
		private async void CheckForUpdatesAsync()
		{
			await Task.Delay(0);
			if (Item?.IsEnabled == true && Item.ShouldCheckForUpdates())
			{
				// Raise an event or call a method to initiate the update process.
				UpdateRequired?.Invoke(this, EventArgs.Empty);

				// Update the LastUpdate time.
				Item.LastUpdate = DateTime.UtcNow;
			}
		}

		/// <summary>
		/// Event raised when it's time to check for updates.
		/// </summary>
		public event EventHandler UpdateRequired;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
