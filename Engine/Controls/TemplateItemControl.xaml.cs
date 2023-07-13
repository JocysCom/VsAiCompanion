using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Companions;
using OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for TemplateItemControl.xaml
	/// </summary>
	public partial class TemplateItemControl : UserControl, INotifyPropertyChanged
	{
		public TemplateItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			AiCompanionComboBox.ItemsSource = Enum.GetValues(typeof(Companions.CompanionType));
			AiCompanionComboBox.SelectedItem = Companions.CompanionType.OpenAI;
			ChatPanel.OnSend += ChatPanel_OnSend;
			ChatPanel.OnStop += ChatPanel_OnStop;

			//SolutionRadioButton.IsEnabled = Global.GetSolutionDocuments != null;
			//ProjectRadioButton.IsEnabled = Global.GetProjectDocuments != null;
			//FileRadioButton.IsEnabled = Global.GetSelectedDocuments != null;
			//SelectionRadioButton.IsEnabled = Global.GetSelection != null;
			BindData();
			InitMacros();
			Global.OnSaveSettings += Global_OnSaveSettings;
			Global.AiModelsUpdated += Global_AiModelsUpdated;
		}

		private void Global_OnSaveSettings(object sender, EventArgs e)
		{
			// Update from previous settings.
			if (_item != null)
				_item.Settings = ChatPanel.MessagesPanel.GetWebSettings();
		}

		private async void ChatPanel_OnSend(object sender, EventArgs e)
		{
			await ClientHelper.Send(_item);
		}

		private void ChatPanel_OnStop(object sender, EventArgs e)
		{
			_item?.StopClients();
		}

		public string CreativityName
		{
			get
			{
				var v = _item?.Creativity;
				if (v is null)
					return "";
				if (v >= 2 - 0.25)
					return "Very Creative";
				if (v >= 1.5 - 0.25)
					return "Creative";
				if (v >= 1 - 0.25)
					return "Balanced";
				if (v >= 0.5 - 0.25)
					return "Precise";
				return "Very Precise";
			}
		}

		#region AI Models.

		public BindingList<string> AiModels { get; set; } = new BindingList<string>();

		private void Global_AiModelsUpdated(object sender, EventArgs e)
		{
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			UpdateAiModels(_item?.AiModel);
		}


		public void UpdateAiModels(params string[] args)
		{
			// Make sure checkbox can display current model.
			var list = Global.AppSettings.OpenAiSettings.AiModels.ToList();
			foreach (var arg in args)
			{
				if (!string.IsNullOrEmpty(arg) && !list.Contains(arg))
					list.Add(arg);
			}
			SettingsHelper.Synchronize(list, AiModels);
		}

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (Global.IsIncompleteSettings())
				return;
			Regex filterRx = null;
			try
			{
				filterRx = new Regex(Global.AppSettings.OpenAiSettings.ModelFilter);
			}
			catch { }
			var client = new Companions.ChatGPT.Client(Global.AppSettings.OpenAiSettings.BaseUrl);
			var models = await client.GetModels();
			var modelIds = models
				.OrderByDescending(x => x.Id)
				.Select(x => x.Id)
				.ToArray();
			if (filterRx != null)
				modelIds = modelIds.Where(x => filterRx.IsMatch(x)).ToArray();
			if (modelIds.Any())
				Global.AppSettings.OpenAiSettings.AiModels = modelIds;
			Global.TriggerAiModelsUpdated();
		}


		#endregion

		public DataOperation[] AutoOperations => (DataOperation[])Enum.GetValues(typeof(DataOperation));

		public Dictionary<AttachmentType, string> DataTypes
		{
			get
			{
				if (_DataTypes == null)
				{
					var values = new AttachmentType[] {
						AttachmentType.Clipboard,
						AttachmentType.Selection,
						AttachmentType.ActiveDocument,
						AttachmentType.SelectedDocuments,
						AttachmentType.ActiveProject,
						AttachmentType.SelectedProject,
						AttachmentType.Solution,
					};
					_DataTypes = ControlsHelper.GetDictionary(values);
				}
				return _DataTypes;
			}
			set => _DataTypes = value;
		}
		Dictionary<AttachmentType, string> _DataTypes;

		public Dictionary<MessageBoxOperation, string> MessageBoxOperations
		{
			get
			{
				if (_DataTypes == null)
				{
					var values = (MessageBoxOperation[])Enum.GetValues(typeof(MessageBoxOperation));
					_MessageBoxOperations = ControlsHelper.GetDictionary(values);
				}
				return _MessageBoxOperations;
			}
			set => _MessageBoxOperations = value;
		}
		Dictionary<MessageBoxOperation, string> _MessageBoxOperations;

		TemplateItem _item;
		public void BindData(TemplateItem item = null)
		{
			var oldItem = _item;
			// Update from previous settings.
			if (_item != null)
			{
				_item.Settings = ChatPanel.MessagesPanel.GetWebSettings();
				_item.PropertyChanged -= _item_PropertyChanged;
			}
			// Set new item.
			_item = item ?? new TemplateItem();
			// Make sure that even custom AiModel old and new item is available to select.
			UpdateAiModels(item?.AiModel, oldItem?.AiModel);
			DataContext = _item;
			_item.PropertyChanged += _item_PropertyChanged;
			OnPropertyChanged(nameof(CreativityName));
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			UpdateAiModels(item?.AiModel);
			IconPanel.BindData(_item);
			ChatPanel.MessagesPanel.SetDataItems(_item.Messages, _item.Settings);
			ChatPanel.IsBusy = _item.IsBusy;
			ChatPanel.UpdateButtons();
			// AutoSend once enabled then...
			if (ItemControlType == ItemType.Task && _item.AutoSend)
			{
				// Disable auto-send so that it won't trigger every time item is bound.
				_item.AutoSend = false;
				_ = Dispatcher.BeginInvoke(new Action(() =>
				{
					_ = ClientHelper.Send(_item);
				}));
			}
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TemplateItem.IsBusy))
			{
				ChatPanel.IsBusy = _item.IsBusy;
				ChatPanel.UpdateButtons();
			}
			else if (e.PropertyName == nameof(TemplateItem.Creativity))
				OnPropertyChanged(nameof(CreativityName));
		}

		private void ListToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsListPanelVisible = !PanelSettings.IsListPanelVisible;
			UpdateListToggleButtonIcon();
		}

		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType ItemControlType
		{
			get => _ItemControlType;
			set
			{
				_ItemControlType = value;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				UpdateBarToggleButtonIcon();
				UpdateListToggleButtonIcon();
				OnPropertyChanged(nameof(BarPanelVisibility));
				IsFavoriteCheckBox.Visibility = value == ItemType.Template ? Visibility.Visible : Visibility.Collapsed;
			}
		}
		private ItemType _ItemControlType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				OnPropertyChanged(nameof(BarPanelVisibility));
				UpdateBarToggleButtonIcon();
			}
		}

		public void UpdateListToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsListPanelVisible ? 0 : 180;
			ListToggleButton.RenderTransform = rt;
			ListToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		public Visibility BarPanelVisibility
			=> PanelSettings.IsBarPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		private void BarToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsBarPanelVisible = !PanelSettings.IsBarPanelVisible;
			UpdateListToggleButtonIcon();
		}

		public void UpdateBarToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsBarPanelVisible ? 90 : 270;
			BarToggleButton.RenderTransform = rt;
			BarToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		#endregion

		#region Macros

		private void PropertiesRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			InitMacros();
		}


		void InitMacros()
		{
			AddKeys(SelectionComboBox, null, AppHelper.GetReplaceMacrosSelection());
			AddKeys(FileComboBox, null, AppHelper.GetReplaceMacrosDocument());
			AddKeys(DateComboBox, null, AppHelper.GetReplaceMacrosDate());
			AddKeys(VsMacrosComboBox, null, Global.GetEnvironmentProperties());
		}

		int alwaysSelectedIndex = -1;

		void AddKeys(ComboBox cb, string name, IEnumerable<PropertyItem> list)
		{
			var options = new List<PropertyItem>();
			options.AddRange(list);
			cb.ItemsSource = options;
			cb.SelectedIndex = alwaysSelectedIndex;
			cb.SelectionChanged += ComboBox_SelectionChanged;
		}
		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var cb = (ComboBox)sender;
			if (cb.SelectedIndex <= alwaysSelectedIndex)
				return;
			var item = (PropertyItem)cb.SelectedItem;
			cb.SelectedIndex = alwaysSelectedIndex;
			InsertText(ChatPanel.DataTextBox, "{" + item.Key + "}");
			// Enable use of macros.
			if (!_item.UseMacros)
				_item.UseMacros = true;
		}

		public static void InsertText(TextBox box, string s, bool activate = false)
		{
			// Check if we need to set the control active
			if (activate)
				box.Focus();
			// Save the current position of the cursor
			var cursorPosition = box.CaretIndex;
			// Check if there is a selected text to replace
			if (box.SelectionLength > 0)
			{
				// Replace the selected text
				box.SelectedText = s;
			}
			else
			{
				// Insert the text at the cursor position
				box.Text = box.Text.Insert(cursorPosition, s);
				// Set the cursor after the inserted text
				box.CaretIndex = cursorPosition + s.Length;
			}
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));



		#endregion

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var head = "Caring for Your Sensitive Data";
			var body = "As you share files for AI processing, please remember not to include confidential, proprietary, or sensitive information.";
			Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentEnumComboBox, head, body, MessageBoxImage.Warning);
			Global.MainControl.InfoPanel.HelpProvider.Add(ContextTypeLabel, head, body, MessageBoxImage.Warning);
			Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentIcon, head, body, MessageBoxImage.Warning);
			if (!Global.IsVsExtesion)
			{
				Global.MainControl.InfoPanel.HelpProvider.Add(FileComboBox, UseMacrosCheckBox.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(SelectionComboBox, UseMacrosCheckBox.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutomationVsLabel, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutoOperationComboBox, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutoFormatCodeCheckBox, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
			}
			Global.MainControl.InfoPanel.HelpProvider.Add(ChatHistoryCheckBox, ChatHistoryCheckBox.Content as string,
				"The AI API doesn't store messages, so the chat log must be attached to each request in order to simulate a conversation.");
			Global.MainControl.InfoPanel.HelpProvider.Add(IsPreviewCheckBox, IsPreviewCheckBox.Content as string,
				ClientHelper.PreviewModeMessage);
			Global.MainControl.InfoPanel.HelpProvider.Add(IsFavoriteCheckBox, IsFavoriteCheckBox.Content as string,
				"Display the template button in the toolbar for quick task creation.");
		}

		private void ClearMessagesButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to clear all messages?";
			var caption = $"{Global.Info.Product} - Clear messages";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			_item.Messages.Clear();
			ChatPanel.MessagesPanel.SetDataItems(_item.Messages, _item.Settings);
			ChatPanel.UpdateButtons();
		}

		private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
		{
			ChatPanel.MessagesPanel.InvokeScript("ScrollToBottom()");
		}

		private void GenerateTitleButton_Click(object sender, RoutedEventArgs e)
		{
			var firstMessage = _item.Messages.FirstOrDefault();
			if (firstMessage == null)
				return;
			var message = new ChatCompletionRequestMessage()
			{
				Name = firstMessage.User,
				Content = $"{firstMessage.BodyInstructions}\r\n\r\n{firstMessage.Body}",
				Role = firstMessage.Type == MessageType.Out
						? ChatCompletionRequestMessageRole.user
						: ChatCompletionRequestMessageRole.assistant,
			};
			var messages = new List<ChatCompletionRequestMessage>() { message };
			_ = ClientHelper.AutoGenerateTitle(_item, messages);
		}
	}
}
