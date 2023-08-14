using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for QuickActionControl.xaml
	/// </summary>
	public partial class QuickActionControl : UserControl
	{
		public QuickActionControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
			{
				var list = new SortableBindingList<TemplateItem>();
				CurrentItems = list;
				return;
			}
			CurrentItems = Global.Templates.Items;
			CurrentItems.ListChanged += CurrentItems_ListChanged;
		}

		public SortableBindingList<TemplateItem> CurrentItems { get; set; }

		private void CurrentItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			switch (e.ListChangedType)
			{
				case ListChangedType.ItemAdded:
					AddButton(CurrentItems[e.NewIndex]);
					break;
				case ListChangedType.ItemDeleted:
					RefreshToolbar();
					break;
				case ListChangedType.Reset:
					MyToolBar.Items.Clear();
					CreateButtons();
					break;
			}
		}


		private void AddButton(TemplateItem item)
		{
			var button = CreateButton(item);
			MyToolBar.Items.Add(button);
			Global.MainControl.InfoPanel.HelpProvider.Add(button, item.Name, item.TextInstructions);
		}

		private void RefreshToolbar()
		{
			foreach (Control button in MyToolBar.Items)
				Global.MainControl.InfoPanel.HelpProvider.Remove(button);
			MyToolBar.Items.Clear();
			CreateButtons();
		}

		/// <summary>
		/// Manually create the buttons because using an `ItemsControl` to generate a list of buttons
		/// adds extra controls that break the toolbar style.
		/// </summary>
		private void CreateButtons()
		{
			foreach (var item in CurrentItems)
				AddButton(item);
		}

		private Button CreateButton(TemplateItem item)
		{
			var buttonTemplate = (DataTemplate)Resources["ToolBarButtonTemplate"];
			var button = (Button)buttonTemplate.LoadContent();
			button.DataContext = item;
			return button;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var item = (TemplateItem)((Button)sender).DataContext;
			var copy = item.Copy(true);
			// Hide instructions box by default on Tasks.
			copy.ShowInstructions = false;
			var panel = Global.MainControl.TasksPanel.ListPanel;
			AppHelper.FixName(copy, Global.Tasks.Items);
			panel.InsertItem(copy);
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				CreateButtons();
			}
		}

		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
		}
	}
}
