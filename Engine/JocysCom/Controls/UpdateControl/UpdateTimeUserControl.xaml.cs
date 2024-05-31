using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Interaction logic for UpdateTimeUserControl.xaml
	/// </summary>
	public partial class UpdateTimeUserControl : UserControl, INotifyPropertyChanged
	{
		public UpdateTimeUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}
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
			}
		}
		UpdateTimeSettings _Item;

		public Dictionary<TimeUnitType, string> TimeUnitTypes =>
			JocysCom.ClassLibrary.Runtime.Attributes.GetDictionary(new[] {
				TimeUnitType.Hour, TimeUnitType.Day, TimeUnitType.Week, TimeUnitType.Month
			});

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
