using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

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
		}

		/// <summary>
		/// The UpdateTimeSettings object that this control binds to.
		/// </summary>
		public UpdateTimeSettings Settings
		{
			get => _Settings;
			set
			{
				_Settings = value;
				OnPropertyChanged();
			}
		}
		UpdateTimeSettings _Settings;

		/// <summary>
		/// Provides a dictionary of time units for use in the frequency unit ComboBox.
		/// </summary>
		public Dictionary<TimeUnitType, string> TimeUnitTypes =>
			JocysCom.ClassLibrary.Runtime.Attributes.GetDictionary(new[] {
				TimeUnitType.Hour, TimeUnitType.Day, TimeUnitType.Week, TimeUnitType.Month
			});


		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
