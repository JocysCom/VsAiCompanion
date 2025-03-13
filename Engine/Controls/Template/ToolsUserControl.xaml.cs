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
		/// <summary>
		/// The template item to be configured with tool choices.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				_Item = value;
				UpdateListBoxData(value?.ToolChoiceRequiredNames);
				UpdateExcludeListBoxData(value?.ToolExcludeAllExceptNames);
				OnPropertyChanged(nameof(ToolChoiceRequiredNamesString));
				OnPropertyChanged(nameof(ToolExcludeAllExceptNamesString));
				OnPropertyChanged();
			}
		}

		private void CheckItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var item = (KeyValue<string, bool>)sender;
			if (item.Value)
			{
				var itemsToUncheck = ListBoxData.Where(x => x.Value && x != item).ToList();
				foreach (var itemToUncheck in itemsToUncheck)
				{
					itemToUncheck.PropertyChanged -= CheckItem_PropertyChanged;
					itemToUncheck.Value = false;
					itemToUncheck.PropertyChanged += CheckItem_PropertyChanged;
				}
			}
			Item.ToolChoiceRequiredNames = ListBoxData.Where(x => x.Value).Select(x => x.Key).ToList();
			OnPropertyChanged(nameof(ToolChoiceRequiredNamesString));
		}

		private void ExcludeCheckItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var item = (KeyValue<string, bool>)sender;
			Item.ToolExcludeAllExceptNames = ExcludeListBoxData.Where(x => x.Value).Select(x => x.Key).ToList();
			OnPropertyChanged(nameof(ToolExcludeAllExceptNamesString));
		}

		/// <summary>
		/// Gets a string representation of the required tool names.
		/// </summary>
		public string ToolChoiceRequiredNamesString => Item?.ToolChoiceRequiredNames.Count > 0
			? "(" + string.Join(", ", Item.ToolChoiceRequiredNames) + ")" : "";

		/// <summary>
		/// Gets a string representation of the excluded tool names.
		/// </summary>
		public string ToolExcludeAllExceptNamesString => Item?.ToolExcludeAllExceptNames.Count > 0
			? "(" + string.Join(", ", Item.ToolExcludeAllExceptNames) + ")" : "";

		/// <summary>
		/// Collection of tool choices for required selection.
		/// </summary>
		public ObservableCollection<KeyValue<string, bool>> ListBoxData { get; set; } = new ObservableCollection<KeyValue<string, bool>>();

		/// <summary>
		/// Collection of tool choices for exclusion selection.
		/// </summary>
		public ObservableCollection<KeyValue<string, bool>> ExcludeListBoxData { get; set; } = new ObservableCollection<KeyValue<string, bool>>();

		void UpdateListBoxData(IList<string> list)
		{
			if (!ListBoxData.Any())
			{
				var checkItems = PluginsManager.GetPluginFunctions()
					.Select(x => new KeyValue<string, bool>() { Key = x.Name, })
					.OrderBy(x => x.Key)
					.ToList();
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
			if (checkedItem != null)
				ToolChoiceRequiredNamesListBox.ScrollIntoView(checkedItem);
		}

		void UpdateExcludeListBoxData(IList<string> list)
		{
			if (!ExcludeListBoxData.Any())
			{
				var checkItems = PluginsManager.GetPluginFunctions()
					.Select(x => new KeyValue<string, bool>() { Key = x.Name, })
					.OrderBy(x => x.Key)
					.ToList();
				foreach (var checkItem in checkItems)
				{
					checkItem.PropertyChanged += ExcludeCheckItem_PropertyChanged;
					ExcludeListBoxData.Add(checkItem);
				}
			}
			foreach (var checkItem in ExcludeListBoxData)
			{
				var isChecked = list != null && list.Contains(checkItem.Key);
				if (checkItem.Value != isChecked)
				{
					checkItem.PropertyChanged -= ExcludeCheckItem_PropertyChanged;
					checkItem.Value = isChecked;
					checkItem.PropertyChanged += ExcludeCheckItem_PropertyChanged;
				}
			}
			var checkedItem = ExcludeListBoxData.Where(x => x.Value).FirstOrDefault();
			if (checkedItem != null)
				ToolExcludeAllExceptNamesListBox.ScrollIntoView(checkedItem);
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
