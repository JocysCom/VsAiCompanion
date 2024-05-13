using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Speech;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for control.
	/// </summary>
	public partial class VoiceListControl : UserControl, INotifyPropertyChanged
	{
		public VoiceListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			FilteredList = Global.Voices.Items;
			OnPropertyChanged(nameof(FilteredList));
		}

		#region ■ Properties

		public SortableBindingList<VoiceItem> FilteredList { get; set; }

		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(MainDataGrid, nameof(VoiceItem.Name), list, 0);
		}

		#endregion

		TaskSettings PanelSettings { get; set; } = Global.AppSettings.GetTaskSettings(ItemType.Voice);

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<Guid>(MainDataGrid, nameof(VoiceItem.Name))
					.Select(x => x.ToString()).Distinct().ToList();
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				var list = PanelSettings.GetSelectionListAsGuid();
				// Try to restore selection.
				ControlsHelper.SetSelection(
					MainDataGrid, nameof(VoiceItem.Name),
					list, PanelSettings.ListSelectedIndex
				);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
