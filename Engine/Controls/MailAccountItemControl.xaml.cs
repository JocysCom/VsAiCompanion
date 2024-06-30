using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
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
			AllowedSenderdsTabPage.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			ValidateSendersCheckBox.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			ValidateDigitalSignatureCheckBox.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			ValidateDkimCheckBox.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
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

		private void Client_LogMessage(object sender, string e)
		{
			ControlsHelper.AppendText(LogTextBox, e + "\r\n");
		}

		public Dictionary<MailConnectionSecurity, string> ConnectionSecurities
			=> ClassLibrary.Runtime.Attributes.GetDictionary(
			(MailConnectionSecurity[])Enum.GetValues(typeof(MailConnectionSecurity)));

		private async void TestImapConnectionButton_Click(object sender, RoutedEventArgs e)
		{
			LogTextBox.Clear();
			LogTabPage.IsSelected = true;
			var client = new AiMailClient();
			client.Account = Item;
			client.LogMessage += Client_LogMessage;
			await client.TestAccount(true);
			client.Account = null;
		}

		private async void TestSmtpConnectionButton_Click(object sender, RoutedEventArgs e)
		{
			LogTextBox.Clear();
			LogTabPage.IsSelected = true;
			var client = new AiMailClient();
			client.Account = Item;
			client.LogMessage += Client_LogMessage;
			await client.TestAccount(false);
			client.Account = null;
		}
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
