using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PromptsControl.xaml
	/// </summary>
	public partial class ListsPromptsControl : UserControl, INotifyPropertyChanged
	{
		public ListsPromptsControl()
		{
			Global.Lists.Items.ListChanged += Global_Lists_Items_Items_ListChanged;
			UpdateListNames();
			InitializeComponent();
		}

		private void Global_Lists_Items_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, UpdateListNames, nameof(ListInfo.Path), nameof(ListInfo.Name));
		}

		TemplateItem Item;

		public void BindData(TemplateItem item = null)
		{
			if (Equals(item, Item))
				return;
			PromptNameComboBox.SelectionChanged -= PromptNameComboBox_SelectionChanged;
			DataContext = null;
			FixPromptName(item);
			var li = Global.Lists.Items.FirstOrDefault(x => x.Name == item.PromptName);
			PromptOptions = li?.Items;
			OnPropertyChanged(nameof(PromptOptions));
			Item = item;
			DataContext = item;
			PromptNameComboBox.SelectionChanged += PromptNameComboBox_SelectionChanged;
		}

		void FixPromptName(TemplateItem item)
		{
			if (!PromptNames.Contains(item.PromptName))
				item.PromptName = PromptNames.FirstOrDefault();
		}

		private void PromptNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Set options.
			var li = e.AddedItems.Cast<ListInfo>().FirstOrDefault();
			PromptOptions = li?.Items;
			OnPropertyChanged(nameof(PromptOptions));
		}

		public BindingList<string> PromptNames { get; } = new BindingList<string>();

		public BindingList<ListItem> PromptOptions { get; set; } = new BindingList<ListItem>();

		public ObservableCollection<ListInfo> PromptLists { get; set; } = new ObservableCollection<ListInfo>();

		private void UpdateListNames()
		{
			// Update ContextListNames
			var names = AppHelper.GetListNames(Item?.Name, "Prompt");
			CollectionsHelper.Synchronize(names, PromptLists);
			OnPropertyChanged(nameof(PromptLists));
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				UpdateListNames();
			}
		}


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

	}
}
