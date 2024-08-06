using HtmlAgilityPack;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Processes;
using JocysCom.VS.AiCompanion.DataClient.Common;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
			Global.PromptingUpdated += Global_PromptingUpdated;
			ChatPanel.OnSend += ChatPanel_OnSend;
			ChatPanel.OnStop += ChatPanel_OnStop;
			ChatPanel.MessagesPanel.WebBrowserDataLoaded += MessagesPanel_WebBrowserDataLoaded;
			ChatPanel.MessagesPanel.ScriptingHandler.OnMessageAction += MessagesPanel_ScriptingHandler_OnMessageAction;
			ChatPanel.DataTextBox.GotFocus += ChatPanel_DataTextBox_GotFocus;
			ChatPanel.DataInstructionsTextBox.GotFocus += ChatPanel_DataTextBox_GotFocus;
			ChatPanel.DataInstructionsTextBox.TextChanged += ChatPanel_DataInstructionsTextBox_TextChanged;
			//SolutionRadioButton.IsEnabled = Global.GetSolutionDocuments != null;
			//ProjectRadioButton.IsEnabled = Global.GetProjectDocuments != null;
			//FileRadioButton.IsEnabled = Global.GetSelectedDocuments != null;
			//SelectionRadioButton.IsEnabled = Global.GetSelection != null;
			Item = null;
			InitMacros();
			Global.OnSaveSettings += Global_OnSaveSettings;
			ChatPanel.UseEnterToSendMessage = Global.AppSettings.UseEnterToSendMessage;
			PromptsPanel.AddPromptButton.Click += PromptsPanel_AddPromptButton_Click;
			ListsPromptsPanel.AddPromptButton.Click += ListsPromptsPanel_AddPromptButton_Click;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			UpdateSpellCheck();
			var checkBoxes = ControlsHelper.GetAll<CheckBox>(this);
			AppHelper.EnableKeepFocusOnMouseClick(checkBoxes);
			// Lists Dropdowns.
			Global.Lists.Items.ListChanged += Global_Lists_Items_Items_ListChanged;
			UpdateListNames();
			// Embeddings dropdown.
			Global.Embeddings.Items.ListChanged += Embeddings_Items_ListChanged;
			UpdateEmbeddingNames();
			// Mails dropdown.
			Global.AppSettings.MailAccounts.ListChanged += MailAccounts_ListChanged;
			UpdateMailAccounts();
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			// Show debug features.
			MonitorInboxCheckBox.Visibility = debugVisibility;
			UseTextToAudioCheckBox.Visibility = debugVisibility;
			UseTextToVideoCheckBox.Visibility = debugVisibility;
			UseAudioToTextCheckBox.Visibility = debugVisibility;
			TemplateAudioToTextComboBox.Visibility = debugVisibility;
			TemplateTextToAudioComboBox.Visibility = debugVisibility;
			TemplateTextToVideoComboBox.Visibility = debugVisibility;
			Global.OnTabControlSelectionChanged += Global_OnTabControlSelectionChanged;
		}

		private void ChatPanel_DataInstructionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateShowInstructionsCheckBox();
		}

		void UpdateShowInstructionsCheckBox()
		{
			var isEmpty = string.IsNullOrWhiteSpace(ChatPanel.DataInstructionsTextBox.Text);
			var fontWeight = isEmpty ? FontWeights.Normal : FontWeights.SemiBold;
			if (ShowInstructionsCheckBox.FontWeight != fontWeight)
			{
				ShowInstructionsCheckBox.FontWeight = fontWeight;
			}
		}

		private void Global_OnTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateAvatarControl();
		}

		private void Global_Lists_Items_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, UpdateListNames, nameof(ListInfo.Path), nameof(ListInfo.Name));
		}

		private void Global_PromptingUpdated(object sender, EventArgs e)
		{
			PromptsPanel.BindData(_Item);
			ListsPromptsPanel.BindData(_Item);
		}

		private void PromptsPanel_AddPromptButton_Click(object sender, RoutedEventArgs e)
		{
			var promptItem = Global.Prompts.Items.FirstOrDefault(x => x.Name == _Item?.PromptName);
			if (promptItem == null)
				return;
			var promptString = string.Format(promptItem.Pattern, _Item?.PromptOption);
			var box = GetFocused();
			AppHelper.InsertText(box, promptString, false, true);
		}

		private void ListsPromptsPanel_AddPromptButton_Click(object sender, RoutedEventArgs e)
		{
			var promptItem = Global.Lists.Items.FirstOrDefault(x => x.Name == _Item?.ListPromptName);
			if (promptItem == null)
				return;
			var promptOption = promptItem.Items.FirstOrDefault(x => x.Key == _Item?.ListPromptOption);
			if (promptOption == null)
				return;
			var box = GetFocused();
			AppHelper.InsertText(box, promptOption.Value, false, true);
		}

		bool WebBrowserDataLoaded;

		private async void MessagesPanel_WebBrowserDataLoaded(object sender, EventArgs e)
		{
			await Helper.Delay(SetZoom, AppHelper.NavigateDelayMs);
			WebBrowserDataLoaded = true;
			RestoreFocus();
		}

		private async void MessagesPanel_ScriptingHandler_OnMessageAction(object sender, string[] e)
		{
			var actionString = e[1];
			if (string.IsNullOrEmpty(actionString))
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), actionString);
			if (action != MessageAction.Use && action != MessageAction.Edit && action != MessageAction.Regenerate)
				return;
			var id = e[0];
			var message = ChatPanel.MessagesPanel.Messages.FirstOrDefault(x => x.Id == id);
			if (message == null)
				return;
			if (action == MessageAction.Use)
			{
				ChatPanel.DataTextBox.Text = message.Body;
				ChatPanel.EditMessageId = null;
				ChatPanel.FocusDataTextBox();
			}
			if (action == MessageAction.Regenerate)
			{
				ChatPanel.EditMessageId = id;
				ChatPanel.FocusDataTextBox();
				var voiceInstructions = GetVoiceInstructions();
				await ClientHelper.Send(_Item, ChatPanel.ApplyMessageEdit, message.Body, extraInstructions: voiceInstructions);
			}
			else if (action == MessageAction.Edit)
			{
				ChatPanel.DataTextBox.Text = message.Body;
				ChatPanel.EditMessageId = id;
				ChatPanel.FocusDataTextBox();
			}
		}

		private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AppData.UseEnterToSendMessage):
					ChatPanel.UseEnterToSendMessage = Global.AppSettings.UseEnterToSendMessage;
					break;
				case nameof(AppData.IsSpellCheckEnabled):
					UpdateSpellCheck();
					break;
				default:
					break;
			}
		}

		#region SpellCheck

		void UpdateSpellCheck()
		{
			var isEnabled = Global.AppSettings.IsSpellCheckEnabled;
			UpdateSpellCheckForTextBox(ChatPanel.DataTextBox, isEnabled);
			UpdateSpellCheckForTextBox(ChatPanel.DataInstructionsTextBox, isEnabled);
		}

		private void UpdateSpellCheckForTextBox(TextBox box, bool isEnabled)
		{
			box.PreviewTextInput -= TextBox_PreviewTextInput;
			if (isEnabled)
				box.PreviewTextInput += TextBox_PreviewTextInput;
			SpellCheck.SetIsEnabled(box, isEnabled);
		}

		private async void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
		{
			var box = (TextBox)sender;
			if (box.SpellCheck.IsEnabled)
				box.SpellCheck.IsEnabled = false;
			if (box == ChatPanel.DataTextBox)
				await Helper.Delay(EnableOnDataTextBox);
			if (box == ChatPanel.DataInstructionsTextBox)
				await Helper.Delay(EnableOnDataInstructionsTextBox);
		}

		void EnableOnDataTextBox()
			=> ChatPanel.DataTextBox.SpellCheck.IsEnabled = true;
		void EnableOnDataInstructionsTextBox()
			=> ChatPanel.DataInstructionsTextBox.SpellCheck.IsEnabled = true;

		#endregion

		private void Global_OnSaveSettings(object sender, EventArgs e)
		{
			// Update from previous settings.
			if (_Item != null)
			{
				var settings = ChatPanel.MessagesPanel.GetWebSettings();
				if (settings != null)
					_Item.Settings = settings;
			}
		}

		private async void ChatPanel_OnSend(object sender, EventArgs e)
		{
			if (_Item != null)
			{
				var voiceInstructions = GetVoiceInstructions();
				await ClientHelper.Send(_Item, ChatPanel.ApplyMessageEdit, extraInstructions: voiceInstructions);
				RestoreFocus();
			}
		}

		string GetVoiceInstructions()
		{
			// If use voice is checked or if avatar is visible.
			if (Item.UseAvatarVoice || Global.IsAvatarInWindow || Item.ShowAvatar)
				return Global.AppSettings.AiAvatar.Instructions;
			return null;
		}

		private void ChatPanel_OnStop(object sender, EventArgs e)
		{
			_Item?.StopClients();
			RestoreFocus();
		}

		public string CreativityName
		{
			get
			{
				var v = _Item?.Creativity;
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

		public DataOperation[] AutoOperations
			=> (DataOperation[])Enum.GetValues(typeof(DataOperation));
		public Dictionary<ToolCallApprovalProcess, string> PluginApprovalProcesses
			=> ClassLibrary.Runtime.Attributes.GetDictionary((ToolCallApprovalProcess[])Enum.GetValues(typeof(ToolCallApprovalProcess)));

		public Dictionary<RiskLevel, string> MaxRiskLevels
			=> ClassLibrary.Runtime.Attributes.GetDictionary(
				((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new[] { RiskLevel.Unknown }).ToArray());

		public Dictionary<string, string> PluginTemplates
			=> Global.Templates.Items.ToDictionary(x => x.Name, x => x.Name);

		public ObservableCollection<ListInfo> ContextListNames { get; set; } = new ObservableCollection<ListInfo>();
		public ObservableCollection<ListInfo> ProfileListNames { get; set; } = new ObservableCollection<ListInfo>();
		public ObservableCollection<ListInfo> RoleListNames { get; set; } = new ObservableCollection<ListInfo>();

		private void UpdateListNames()
		{
			// Update ContextListNames
			var names = AppHelper.GetListNames(Item?.Name, "Context", "Company", "Department");
			CollectionsHelper.Synchronize(names, ContextListNames);
			OnPropertyChanged(nameof(ContextListNames));
			// Update ProfileListNames
			names = AppHelper.GetListNames(Item?.Name, "Profile", "Persona");
			CollectionsHelper.Synchronize(names, ProfileListNames);
			OnPropertyChanged(nameof(ProfileListNames));
			// Update RoleListNames
			names = AppHelper.GetListNames(Item?.Name, "Role");
			CollectionsHelper.Synchronize(names, RoleListNames);
			OnPropertyChanged(nameof(RoleListNames));
		}

		public ObservableCollection<EnumComboBox.CheckBoxViewModel> AttachContexts
		{
			get
			{
				if (_AttachContexts == null)
					_AttachContexts = EnumComboBox.GetItemSource<ContextType>();
				return _AttachContexts;
			}
			set => _AttachContexts = value;
		}
		ObservableCollection<EnumComboBox.CheckBoxViewModel> _AttachContexts;

		public Dictionary<MessageBoxOperation, string> MessageBoxOperations
		{
			get
			{
				if (_MessageBoxOperations == null)
				{
					var values = (MessageBoxOperation[])Enum.GetValues(typeof(MessageBoxOperation));
					_MessageBoxOperations = ClassLibrary.Runtime.Attributes.GetDictionary(values);
				}
				return _MessageBoxOperations;
			}
			set => _MessageBoxOperations = value;
		}
		Dictionary<MessageBoxOperation, string> _MessageBoxOperations;

		TemplateItem _Item;
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				var oldItem = _Item;
				// Update from previous settings.
				if (_Item != null)
				{
					_Item.PropertyChanged -= _item_PropertyChanged;
					_Item.Settings = ChatPanel.MessagesPanel.GetWebSettings();
				}
				// Make sure that custom AiModel old and new item is available to select.
				AppHelper.UpdateModelCodes(value?.AiService, AiModelBoxPanel.AiModels, value?.AiModel, oldItem?.AiModel);
				// Set new item.
				_Item = value ?? AppHelper.GetNewTemplateItem(true);
				// This will trigger AiCompanionComboBox_SelectionChanged event.
				AiModelBoxPanel.Item = null;
				ChatPanel.AttachmentsPanel.CurrentItems = null;
				DataContext = _Item;
				_Item.PropertyChanged += _item_PropertyChanged;
				AiModelBoxPanel.Item = _Item;
				OnPropertyChanged(nameof(CreativityName));
				// New item is bound. Make sure that custom AiModel only for the new item is available to select.
				AppHelper.UpdateModelCodes(_Item.AiService, AiModelBoxPanel.AiModels, _Item?.AiModel);
				PluginApprovalPanel.Item = _Item.PluginFunctionCalls;
				ChatPanel.AttachmentsPanel.CurrentItems = _Item.Attachments;
				IconPanel.BindData(_Item);
				PromptsPanel.BindData(_Item);
				ListsPromptsPanel.BindData(_Item);
				ChatPanel.MessagesPanel.SetDataItems(_Item.Messages, _Item.Settings);
				ChatPanel.IsBusy = _Item.IsBusy;
				ChatPanel.UpdateMessageEdit();
				System.Diagnostics.Debug.WriteLine($"Bound Item: {_Item.Name}");
				// AutoSend once enabled then...
				if (DataType == ItemType.Task && _Item.AutoSend)
				{
					// Disable auto-send so that it won't trigger every time item is bound.
					_Item.AutoSend = false;
					ControlsHelper.AppBeginInvoke(() =>
					{
						var voiceInstructions = GetVoiceInstructions();
						_ = ClientHelper.Send(_Item, ChatPanel.ApplyMessageEdit, extraInstructions: voiceInstructions);
					});
				}
				_ = Helper.Delay(EmbeddingGroupFlags_OnPropertyChanged);
				if (PanelSettings.Focus)
					RestoreFocus();
				UpdateShowInstructionsCheckBox();
				UpdateAvatarControl();
			}
		}

		// Move to settings later.
		public const string TextToProcess = "Text to process:";

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TemplateItem.IsBusy):
					ChatPanel.IsBusy = _Item.IsBusy;
					ChatPanel.UpdateMessageEdit();
					break;
				case nameof(TemplateItem.Creativity):
					OnPropertyChanged(nameof(CreativityName));
					break;
				case nameof(TemplateItem.IsSystemInstructions):
					var text = _Item.TextInstructions.Trim();
					var containsDataHeader = text.Contains(TextToProcess) || text.EndsWith(":");
					if (_Item.IsSystemInstructions && text.Contains(TextToProcess))
					{
						var s = text.Replace(TextToProcess, "").TrimEnd();
						AppHelper.SetText(ChatPanel.DataInstructionsTextBox, s);
					}
					else if (!_Item.IsSystemInstructions && !containsDataHeader && !string.IsNullOrEmpty(text))
					{
						var s = ClientHelper.JoinMessageParts(text, TextToProcess);
						AppHelper.SetText(ChatPanel.DataInstructionsTextBox, s);
					}
					break;
				case nameof(TemplateItem.ShowAvatar):
					UpdateAvatarControl();
					break;
				case nameof(TemplateItem.EmbeddingGroupName):
					_ = Helper.Delay(EmbeddingGroupFlags_OnPropertyChanged);
					break;
				default:
					break;
			}
		}

		#region ■ Properties

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
				ZoomSlider.Value = PanelSettings.ChatPanelZoom;
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				OnPropertyChanged(nameof(BarPanelVisibility));
				OnPropertyChanged(nameof(TemplateItemVisibility));
			}
		}
		private ItemType _DataType;

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
			AppHelper.InsertText(ChatPanel.DataTextBox, "{" + item.Key + "}");
			// Enable use of macros.
			if (!_Item.UseMacros)
				_Item.UseMacros = true;
		}

		#endregion

		public void UpdateAvatarControl()
		{
			Global.UpdateAvatarControl(ChatPanel.AvatarPanelBorder, Item?.ShowAvatar == true);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				var head = "Caring for Your Sensitive Data";
				var body = "As you share files for AI processing, please remember not to include confidential, proprietary, or sensitive information.";
				Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentEnumComboBox, head, body, MessageBoxImage.Warning);
				Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentIcon, head, body, MessageBoxImage.Warning);
				Global.MainControl.InfoPanel.HelpProvider.Add(ContextTypeLabel, head, body, MessageBoxImage.Warning);
				if (!Global.IsVsExtension)
				{
					Global.MainControl.InfoPanel.HelpProvider.Add(FileComboBox, UseMacrosCheckBox.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(SelectionComboBox, UseMacrosCheckBox.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutomationVsLabel, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutoOperationComboBox, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutoFormatCodeCheckBox, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
				}
				CodeBlockPanel.GetFocused = GetFocused;
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
				// Remove control, which visibility is controlled by the code.
				UiPresetsManager.RemoveControls(this, MainTabControl);
			}
			RestoreFocus();
			UpdateAvatarControl();
		}

		private void ClearMessagesButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to clear all messages?";
			var caption = $"{Global.Info.Product} - Clear messages";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			_Item.Messages.Clear();
			ChatPanel.MessagesPanel.SetDataItems(_Item.Messages, _Item.Settings);
			ChatPanel.UpdateMessageEdit();
			RestoreFocus();
		}

		private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
		{
			ChatPanel.MessagesPanel.InvokeScript("ScrollToBottom()");
			RestoreFocus();
		}

		private void GenerateTitleButton_Click(object sender, RoutedEventArgs e)
		{
			var firstMessage = _Item.Messages.FirstOrDefault();
			if (firstMessage == null)
				return;
			_ = ClientHelper.GenerateTitle(_Item);
		}

		private void HyperLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			ControlsHelper.OpenUrl(e.Uri.AbsoluteUri);
			e.Handled = true;
		}

		private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			PanelSettings.ChatPanelZoom = (int)ZoomSlider.Value;
		}

		void SetZoom()
		{
			ControlsHelper.AppInvoke(() =>
			{
				ChatPanel.MessagesPanel.SetZoom((int)ZoomSlider.Value);
			});
		}

		#region Focus

		private TextBox LastFocusedForCodeTextBox;

		private void ChatPanel_DataTextBox_GotFocus(object sender, RoutedEventArgs e)
		{
			LastFocusedForCodeTextBox = (TextBox)sender;
			PanelSettings.SaveFocus();
		}

		private TextBox GetFocused()
		{
			var box = _Item?.ShowInstructions == true
				? LastFocusedForCodeTextBox ?? ChatPanel.DataInstructionsTextBox
				: ChatPanel.DataTextBox;
			return box;
		}

		private void RestoreFocus()
		{
			// Note: Setting focus during web browser loading fails to hide textbox placeholder.
			if (WebBrowserDataLoaded)
				_ = Helper.Delay(_RestoreFocus, AppHelper.NavigateDelayMs);
		}

		private void _RestoreFocus()
		{
			var box = GetFocused();
			var canFocus = PanelSettings.FocusedControl != ChatPanel.DataInstructionsTextBox.Name || _Item.ShowInstructions;
			if (canFocus)
			{
				PanelSettings.RestoreFocus(ChatPanel);
			}
		}

		#endregion

		#region PanelSettings

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				OnPropertyChanged(nameof(BarPanelVisibility));
				OnPropertyChanged(nameof(TemplateItemVisibility));
			}
			if (e.PropertyName == nameof(PanelSettings.ChatPanelZoom))
			{
				await Helper.Delay(SetZoom);
			}
		}

		private void ListToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		public Visibility BarPanelVisibility
			=> PanelSettings.IsBarPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		public Visibility TemplateItemVisibility
			=> PanelSettings.IsBarPanelVisible && _DataType == ItemType.Template ? Visibility.Visible : Visibility.Collapsed;

		private void BarToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton, true);
		}

		#endregion

		private void IsSpellCheckEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			Global.AppSettings.IsSpellCheckEnabled = true;
		}

		private void IsSpellCheckEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			Global.AppSettings.IsSpellCheckEnabled = false;
		}

		#region Embeddings

		public ObservableCollection<string> EmbeddingNames { get; set; } = new ObservableCollection<string>();

		public void UpdateEmbeddingNames()
		{
			var names = Global.Embeddings.Items.Select(x => x.Name).ToList();
			if (!names.Contains(""))
				names.Insert(0, "");
			CollectionsHelper.Synchronize(names, EmbeddingNames);
			OnPropertyChanged(nameof(EmbeddingNames));
		}

		public ObservableCollection<EnumComboBox.CheckBoxViewModel> EmbeddingGroupFlags
		{
			get
			{
				if (_EmbeddingGroupFlags == null)
					_EmbeddingGroupFlags = EnumComboBox.GetItemSource<EmbeddingGroupFlag>();
				EmbeddingHelper.ApplyDatabase(Item?.UseEmbeddings == true ? Item?.EmbeddingName : null, _EmbeddingGroupFlags);
				return _EmbeddingGroupFlags;
			}
			set => _EmbeddingGroupFlags = value;
		}
		ObservableCollection<EnumComboBox.CheckBoxViewModel> _EmbeddingGroupFlags;

		private void Embeddings_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			var update = false;
			if (e.ListChangedType == ListChangedType.ItemChanged && e.PropertyDescriptor?.Name == nameof(TemplateItem.EmbeddingName))
				update = true;
			if (e.ListChangedType == ListChangedType.ItemAdded ||
				e.ListChangedType == ListChangedType.ItemDeleted)
				update = true;
			if (update)
				_ = Helper.Delay(UpdateEmbeddingNames);
		}

		public Dictionary<EmbeddingGroupFlag, string> FilePartGroups
			=> ClassLibrary.Runtime.Attributes.GetDictionary(
				(EmbeddingGroupFlag[])Enum.GetValues(typeof(EmbeddingGroupFlag)));

		public void EmbeddingGroupFlags_OnPropertyChanged()
		{
			OnPropertyChanged(nameof(EmbeddingGroupFlags));
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
				_ = Helper.Delay(UpdateMailAccounts);
		}

		#endregion

		private void AttachmentsButton_Click(object sender, RoutedEventArgs e)
		{
			//ChatPanel.AttachmentsPanel.AddFile();
			var files = ChatPanel.AttachmentsPanel.GetFiles();
			if (files == null || !files.Any())
				return;
			var textBox = GetFocused();
			AppControlsHelper.DropFiles(textBox, files);
		}

		private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
		{
			var path = System.IO.Path.Combine(AppHelper.GetTempPath(), "Screenshots");
			var isCtrlDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
			if (isCtrlDown && !Global.IsVsExtension)
				Global.TrayManager.MinimizeToTray(false, Global.AppSettings.MinimizeToTray);
			var captureResult = await ScreenshotHelper.CaptureRegion(null, path, System.Drawing.Imaging.ImageFormat.Jpeg);
			if (captureResult.Success)
			{
				var box = GetFocused();
				AppHelper.InsertText(box, $"Please analyse screenshot\r\n{captureResult.Data}", true, false);
			}
			if (isCtrlDown && !Global.IsVsExtension)
				Global.TrayManager.RestoreFromTray(true, false);
		}


		private void MicrophoneButton_Click(object sender, RoutedEventArgs e)
		{
			var box = GetFocused();
			AppHelper.InsertText(box, "", true);
			// First, press and hold the 'Windows' key.
			KeyboardHelper.SendDown(Key.LWin);
			// Then, press and release the 'H' key.
			KeyboardHelper.Send(Key.H);
			// Finally, release the 'Windows' key.
			KeyboardHelper.SendUp(Key.LWin);

		}

		private void SendChatHistoryCheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			var box = sender as CheckBox;
			if (Item.PluginsEnabled)
			{
				// Set IsChecked back to true
				box.IsChecked = true;
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error, Engine.Resources.MainResources.main_Plugins_require_chat_history);
			}
		}

		#region Copy and Save

		System.Windows.Forms.SaveFileDialog ExportSaveFileDialog { get; } = new System.Windows.Forms.SaveFileDialog();

		private void SaveAsButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = ExportSaveFileDialog;
			dialog.DefaultExt = "*.html";
			dialog.FileName = $"{Item.Name}.html";
			dialog.Filter = "Webpage, single file (*.html)|*.html|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Export HTML File";
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			// Cast the document to an HTMLDocument
			var html = GetPageHtml();
			if (string.IsNullOrEmpty(html))
				return;
			var bytes = System.Text.Encoding.UTF8.GetBytes(html);
			JocysCom.ClassLibrary.Configuration.SettingsHelper.WriteIfDifferent(dialog.FileName, bytes);
		}

		public string GetPageHtml()
		{
			// Cast the document to an HTMLDocument
			var html = ChatPanel.MessagesPanel.InvokeScript("document.documentElement.outerHTML;");
			if (!string.IsNullOrEmpty(html))
				html = CleanupHtml(html);
			return html;
		}

		public static string CleanupHtml(string htmlContent)
		{
			// Load the HTML content into an HtmlDocument
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(htmlContent);
			// Select all <script> nodes with the defer attribute
			var nodes = htmlDoc.DocumentNode.SelectNodes("//script[@defer='defer']");
			if (nodes != null)
				foreach (var node in nodes)
					node.Remove();
			// Select all <button> nodes
			nodes = htmlDoc.DocumentNode.SelectNodes("//button");
			if (nodes != null)
				foreach (var node in nodes)
					node.Remove();
			// Convert the document back to a string without the selected <script> tags
			using (StringWriter writer = new StringWriter())
			{
				htmlDoc.Save(writer);
				return writer.ToString();
			}
		}

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			var html = GetPageHtml();
			AppHelper.SetClipboardHtml(html);
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
