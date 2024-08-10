using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for ToolsUserControl.xaml
	/// </summary>
	public partial class ToolsUserControl : UserControl, INotifyPropertyChanged
	{
		public ToolsUserControl()
		{
			InitializeComponent();
		}

		TemplateItem _Item;
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				_Item = value;
				UpdateListBoxData(value?.ToolChoiceRequiredNames);
				OnPropertyChanged(nameof(ToolChoiceRequiredNamesString));
				OnPropertyChanged();
			}
		}

		private void CheckItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			Item.ToolChoiceRequiredNames = ListBoxData.Where(x => x.Value).Select(x => x.Key).ToList();
			OnPropertyChanged(nameof(ToolChoiceRequiredNamesString));
		}

		public string ToolChoiceRequiredNamesString => Item?.ToolChoiceRequiredNames.Count > 0
			? "(" + string.Join(", ", Item.ToolChoiceRequiredNames) + ")" : "";

		public ObservableCollection<KeyValue<string, bool>> ListBoxData { get; set; } = new ObservableCollection<KeyValue<string, bool>>();

		void UpdateListBoxData(IList<string> list)
		{
			if (!ListBoxData.Any())
			{
				var checkItems = PluginsManager.PluginFunctions.Select(x => new KeyValue<string, bool>()
				{
					Key = x.Key,
				}).ToList();
				foreach (var checkItem in checkItems)
				{
					checkItem.PropertyChanged += CheckItem_PropertyChanged;
					ListBoxData.Add(checkItem);

				}
			}
			foreach (var checkItem in ListBoxData)
			{
				var isChecked = list != null && list.Contains(checkItem.Key);
				if (checkItem.Value != isChecked)
				{
					checkItem.PropertyChanged -= CheckItem_PropertyChanged;
					checkItem.Value = isChecked;
					checkItem.PropertyChanged += CheckItem_PropertyChanged;
				}
			}
			var checkedItem = ListBoxData.Where(x => x.Value).FirstOrDefault();
			ToolChoiceRequiredNamesListBox.SelectedItem = checkedItem;
			if (checkedItem != null)
				ToolChoiceRequiredNamesListBox.ScrollIntoView(checkedItem);
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion


	}
}
