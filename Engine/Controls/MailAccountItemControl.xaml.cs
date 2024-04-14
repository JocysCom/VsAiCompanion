using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for EmbeddingControl.xaml
	/// </summary>
	public partial class MailAccountItemControl : UserControl, INotifyPropertyChanged
	{
		public MailAccountItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			ControlsHelper.EnableAutoScroll(LogTextBox);
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion


		public MailAccount Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
					PasswordPasswordBox.PasswordChanged -= PasswordPasswordBox_PasswordChanged;
				}
				_Item = value;
				if (_Item != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
					PasswordPasswordBox.Password = _Item.Password;
					PasswordPasswordBox.PasswordChanged += PasswordPasswordBox_PasswordChanged;
				}
				LogTextBox.Clear();
				OnPropertyChanged(nameof(Item));
			}
		}
		MailAccount _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingsItem.Source))
			{
			}
		}

		private void PasswordPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		=> Item.Password = PasswordPasswordBox.Password;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			LogTabPage.IsSelected = true;
			var client = new AiMailClient(Item);
			client.LogMessage += Client_LogMessage;
			await client.TestAccount();
			client.Account = null;
		}

		private void Client_LogMessage(object sender, string e)
		{
			ControlsHelper.AppendText(LogTextBox, e + "\r\n");
		}

	}
}
