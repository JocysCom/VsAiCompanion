﻿using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for VaultItemControl.xaml
	/// </summary>
	public partial class VaultItemControl : UserControl, INotifyPropertyChanged
	{
		public VaultItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
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


		public VaultItem Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
					UpdateTimePanel.Settings = null;
				}
				_Item = value;
				if (_Item != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
					ValuePasswordBox.Password = value.Value;
					UpdateTimePanel.Settings = value.UpdateTimeSettings;
				}
				OnPropertyChanged(nameof(Item));
			}
		}
		VaultItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(VaultItem.Value))
				ValuePasswordBox.Password = Item.Value;
		}

		/// <summary>
		/// Stores cancellation tokens created on this control that can be stopped with the [Stop] button.
		/// </summary>
		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			_ = await MicrosoftResourceManager.Current.RefreshItemFromKeyVaultSecret(Item.Id, cancellationTokenSources);
		}

		private void CopyButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var value = Item?.Value;
			if (!string.IsNullOrEmpty(value))
				System.Windows.Clipboard.SetText(value);
		}

		private void ClearVaultItemButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Item?.Clear();
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Global.UserProfile.PropertyChanged += Profile_PropertyChanged;
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
		}

		private void Profile_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserProfile.IsSignedIn))
				OnPropertyChanged(nameof(UserIsSigned));
		}

		public bool UserIsSigned => Global.UserProfile.IsSignedIn;


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
