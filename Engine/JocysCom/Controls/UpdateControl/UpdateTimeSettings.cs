using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.Linq;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Represents the settings related to the timing of update checks.
	/// </summary>
	public class UpdateTimeSettings : NotifyPropertyChanged
	{
		public UpdateTimeSettings()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Gets or sets a value indicating whether automatic update checks are enabled.
		/// </summary>
		[DefaultValue(false)]
		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		/// <summary>
		/// The UTC date and time when the last update check occurred.
		/// </summary>
		[DefaultValue(null)]
		public DateTime? LastUpdate { get => _LastUpdate; set => SetProperty(ref _LastUpdate, value); }
		DateTime? _LastUpdate;

		/// <summary>
		/// Serializes the LastUpdate property only if it has a non-default value.
		/// </summary>
		public bool ShouldSerializeLastUpdate() => LastUpdate != null;

		/// <summary>
		/// The unit of time used for determining the update check frequency (e.g., Day, Week).
		/// </summary>
		[DefaultValue(TimeUnitType.Day)]
		public TimeUnitType CheckFrequencyUnit { get => _CheckFrequencyUnit; set => SetProperty(ref _CheckFrequencyUnit, value); }
		TimeUnitType _CheckFrequencyUnit;

		/// <summary>
		/// The numeric value representing how often to check for updates based on the specified unit.
		/// </summary>
		[DefaultValue(1)]
		public int CheckFrequencyValue { get => _CheckFrequencyValue; set => SetProperty(ref _CheckFrequencyValue, value); }
		int _CheckFrequencyValue;

		/// <summary>
		/// If true, the application checks for updates when it starts.
		/// </summary>
		[DefaultValue(false)]
		public bool CheckOnAppStart { get => _CheckOnAppStart; set => SetProperty(ref _CheckOnAppStart, value); }
		bool _CheckOnAppStart;

		/// <summary>
		/// Determines whether it's time to check for updates based on the last update time and frequency settings.
		/// </summary>
		/// <returns>True if the application should check for updates; otherwise, false.</returns>
		public bool ShouldCheckForUpdates()
		{
			DateTime nextUpdate = GetNextUpdate();
			if (DateTime.Now >= nextUpdate)
				return true;
			return false;
		}

		/// <summary>
		/// Calculates the next scheduled update check time based on the last update and frequency settings.
		/// </summary>
		/// <returns>The DateTime representing the next update check.</returns>
		public DateTime GetNextUpdate()
		{
			return TimeUnitHelper.GetDateTimes(LastUpdate ?? DateTime.MinValue, CheckFrequencyValue, CheckFrequencyUnit).Last();
		}

	}
}
