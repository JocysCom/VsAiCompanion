using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
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
	/// Interaction logic for MailControl.xaml
	/// </summary>
	public partial class MailControl : UserControl, INotifyPropertyChanged
	{
		public MailControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Mails dropdown.
			Global.AppSettings.MailAccounts.ListChanged += MailAccounts_ListChanged;
			UpdateMailAccounts();
			// Show debug features.
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			MonitorInboxCheckBox.Visibility = debugVisibility;
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
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
			}
			_Item = item;
			if (_Item != null)
			{
				_Item.PropertyChanged += _item_PropertyChanged;
			}
			OnPropertyChanged(nameof(Item));
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				default:
					break;
			}
		}

		#endregion

		#region Mail

		public ObservableCollection<string> MailAccounts { get; set; } = new ObservableCollection<string>();

		public void UpdateMailAccounts()
		{
			var names = Global.AppSettings.MailAccounts.Select(x => x.Name).ToList();
			if (!names.Contains(""))
				names.Insert(0, "");
			CollectionsHelper.Synchronize(names, MailAccounts);
			OnPropertyChanged(nameof(MailAccounts));
		}

		private void MailAccounts_ListChanged(object sender, ListChangedEventArgs e)
		{
			var update = false;
			if (e.ListChangedType == ListChangedType.ItemChanged && e.PropertyDescriptor?.Name == nameof(MailAccount.Name))
				update = true;
			if (e.ListChangedType == ListChangedType.ItemAdded ||
				e.ListChangedType == ListChangedType.ItemDeleted)
				update = true;
			if (update)
				_ = Helper.Debounce(UpdateMailAccounts);
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
				UiPresetsManager.InitControl(this, true);
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
