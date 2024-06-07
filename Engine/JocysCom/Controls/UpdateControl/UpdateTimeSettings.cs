using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.Linq;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	public class UpdateTimeSettings : NotifyPropertyChanged
	{
		public UpdateTimeSettings()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Check Is enabled
		/// </summary>
		[DefaultValue(false)]
		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		/// <summary>
		/// UTC time of last update.
		/// </summary>
		[DefaultValue(null)]
		public DateTime? LastUpdate { get => _LastUpdate; set => SetProperty(ref _LastUpdate, value); }
		DateTime? _LastUpdate;

		/// <summary>Serializes property only with non-default values.</summary>
		public bool ShouldSerializeLastUpdate() => LastUpdate != null;

		/// <summary>
		/// Check frequency unit.
		/// </summary>
		[DefaultValue(TimeUnitType.Day)]
		public TimeUnitType CheckFrequencyUnit { get => _CheckFrequencyUnit; set => SetProperty(ref _CheckFrequencyUnit, value); }
		TimeUnitType _CheckFrequencyUnit;

		/// <summary>
		/// Check frequency value.
		/// </summary>
		[DefaultValue(1)]
		public int CheckFrequencyValue { get => _CheckFrequencyValue; set => SetProperty(ref _CheckFrequencyValue, value); }
		int _CheckFrequencyValue;

		/// <summary>
		/// Check on application start.
		/// </summary>
		[DefaultValue(false)]
		public bool CheckOnAppStart { get => _CheckOnAppStart; set => SetProperty(ref _CheckOnAppStart, value); }
		bool _CheckOnAppStart;

		/// <summary>
		/// Returns true if update 
		/// </summary>
		public bool ShouldCheckForUpdates()
		{
			DateTime nextUpdate = GetNextUpdate();
			if (DateTime.Now >= nextUpdate)
				return true;
			return false;
		}

		public DateTime GetNextUpdate()
		{
			return TimeUnitHelper.GetDateTimes(LastUpdate ?? DateTime.MinValue, CheckFrequencyValue, CheckFrequencyUnit).Last();
		}

	}
}
