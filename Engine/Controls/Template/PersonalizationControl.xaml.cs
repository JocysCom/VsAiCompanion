using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for PersonalizationControl.xaml
	/// </summary>
	public partial class PersonalizationControl : UserControl, INotifyPropertyChanged
	{
		public PersonalizationControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Lists Dropdowns.
			Global.Lists.Items.ListChanged += Global_Lists_Items_Items_ListChanged;
			UpdateListNames();
		}

		#region ■ Properties

		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
		}
		TemplateItem _Item;

		public async Task BindData(TemplateItem item)
		{
			await Task.Delay(0);
			if (Equals(item, _Item))
				return;
			var oldItem = _Item;
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
			}
			UpdateListNames(new string[] { item?.Name, oldItem?.Name });
			_Item = item;
			UpdateListNames(new string[] { _Item?.Name, });
			if (_Item != null)
			{
				_Item.PropertyChanged += _item_PropertyChanged;
			}
			UpdateListEditButtons();
			OnPropertyChanged(nameof(Item));
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TemplateItem.Context0ListName):
				case nameof(TemplateItem.Context1ListName):
				case nameof(TemplateItem.Context2ListName):
				case nameof(TemplateItem.Context3ListName):
				case nameof(TemplateItem.Context4ListName):
				case nameof(TemplateItem.Context5ListName):
				case nameof(TemplateItem.Context6ListName):
				case nameof(TemplateItem.Context7ListName):
				case nameof(TemplateItem.Context8ListName):
					UpdateListEditButtons();
					break;
				default:
					break;
			}
		}

		#endregion

		private void Global_Lists_Items_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, UpdateListNames, nameof(ListInfo.Path), nameof(ListInfo.Name));
		}

		public ObservableCollection<ListInfo> ContextListNames { get; set; } = new ObservableCollection<ListInfo>();
		public ObservableCollection<ListInfo> ProfileListNames { get; set; } = new ObservableCollection<ListInfo>();
		public ObservableCollection<ListInfo> RoleListNames { get; set; } = new ObservableCollection<ListInfo>();

		private void UpdateListNames()
		{
			var name = Item?.Name;
			if (string.IsNullOrEmpty(name))
				UpdateListNames(new string[] { });
			else
				UpdateListNames(new string[] { name });
		}

		private void UpdateListNames(string[] extraPaths)
		{
			// Update ContextListNames
			var names = AppHelper.GetListNames(extraPaths, "Context", "Company", "Department");
			CollectionsHelper.Synchronize(names, ContextListNames);
			OnPropertyChanged(nameof(ContextListNames));
			// Update ProfileListNames
			names = AppHelper.GetListNames(extraPaths, "Profile", "Persona");
			CollectionsHelper.Synchronize(names, ProfileListNames);
			OnPropertyChanged(nameof(ProfileListNames));
			// Update RoleListNames
			names = AppHelper.GetListNames(extraPaths, "Role");
			CollectionsHelper.Synchronize(names, RoleListNames);
			OnPropertyChanged(nameof(RoleListNames));
		}

		void UpdateListEditButtons()
		{
			var dic = new Dictionary<Button, string>()
			{
				{ Context0EditButton, Item?.Context0ListName },
				{ Context1EditButton, Item?.Context1ListName },
				{ Context2EditButton, Item?.Context2ListName },
				{ Context3EditButton, Item?.Context3ListName },
				{ Context4EditButton, Item?.Context4ListName },
				{ Context5EditButton, Item?.Context5ListName },
				{ Context6EditButton, Item?.Context6ListName },
				{ Context7EditButton, Item?.Context7ListName },
				{ Context8EditButton, Item?.Context8ListName }
			};
			foreach (var button in dic.Keys.ToArray())
			{
				var enabled = !string.IsNullOrEmpty(dic[button]);
				ControlsHelper.SetEnabled(button, enabled);

				var visibility = enabled ? Visibility.Visible : Visibility.Hidden;
				if (button.Visibility != visibility)
					button.Visibility = visibility;
			}
		}

		#region Open List

		private void Context0EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context0ListName);
		private void Context1EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context1ListName);
		private void Context2EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context2ListName);
		private void Context3EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context3ListName);
		private void Context4EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context4ListName);
		private void Context5EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context5ListName);
		private void Context6EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context6ListName);
		private void Context7EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context7ListName);
		private void Context8EditButton_Click(object sender, RoutedEventArgs e) => OpenListItem(Item.Context8ListName);

		void OpenListItem(string name)
		{
			if (string.IsNullOrEmpty(name))
				return;
			var grid = Global.MainControl.ListsPanel.ListPanel.MainDataGrid;
			ControlsHelper.EnsureTabItemSelected(grid);
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(grid, nameof(ISettingsListFileItem.Name), list, 0);
			_ = Helper.Debounce(() =>
			{
				Global.MainControl.ListsPanel.ListsItemPanel?.InstructionsTextBox.Focus();
			});
		}

		#endregion

		/// <summary>
		/// Handles the Loaded event of the user control.
		/// Initializes help content and UI presets when the control is loaded.
		/// </summary>
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				// Remove control, which visibility is controlled by the code.
				var excludeElements = new FrameworkElement[] {
					Context0EditButton,
					Context1EditButton,
					Context2EditButton,
					Context3EditButton,
					Context4EditButton,
					Context5EditButton,
					Context6EditButton,
					Context7EditButton,
					Context8EditButton,
 				};
				UiPresetsManager.InitControl(this, excludeElements: excludeElements);
			}
		}

		/// <summary>
		/// Handles the Unloaded event of the user control.
		/// Add any necessary cleanup logic here.
		/// </summary>
		private void This_Unloaded(object sender, RoutedEventArgs e)
		{
			// Cleanup logic can be added here if necessary.
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
