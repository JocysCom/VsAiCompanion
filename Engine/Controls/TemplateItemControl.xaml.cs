using DocumentFormat.OpenXml.Drawing.Diagrams;
using HtmlAgilityPack;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Processes;
using JocysCom.VS.AiCompanion.DataClient.Common;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
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
using TheArtOfDev.HtmlRenderer.PdfSharp;

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
			ChatPanel.MessagesPanel.WebBrowserDataLoaded += ChatPanel_MessagesPanel_WebBrowserDataLoaded;
			ChatPanel.MessagesPanel.ScriptingHandler.OnMessageAction += ChatPanel_MessagesPanel_ScriptingHandler_OnMessageAction;
			ChatPanel.SelectionSaved += ChatPanel_SelectionSaved;
			ChatPanel.PropertyChanged += ChatPanel_PropertyChanged;
			ChatPanel.MainTabControl.SelectionChanged += ChatPanel_MainTabControl_SelectionChanged;
			ChatPanel.InitTokenCounters();
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
			Global.Templates.Items.ListChanged += Global_Templates_Items_ListChanged;
		}

		private void Global_Templates_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, () =>
			{
				OnPropertyChanged(nameof(GenerateTitleTemplates));
				OnPropertyChanged(nameof(PluginTemplates));
			});
		}

		private void ChatPanel_MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var tab = ChatPanel.MainTabControl.SelectedItem as TabItem;
			if (tab == null)
				return;
			var risenType = ChatPanel.SelectionControls?
				.FirstOrDefault(x => x.Tab == tab).RisenType ?? RisenType.None;
			// Filter prompts by risen type.
			PromptsPanel.BindData(_Item, risenType);
		}

		private void ChatPanel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ChatPanel.IsChatInputExpanded))
			{
				// If top bar panel is visible then...
				if (PanelSettings.IsBarPanelVisible)
					// Colapse it to make more space for chat input.
					PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton, true);
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
			var box = ChatPanel.GetFocusedTextBox();
			AppHelper.InsertText(box, promptString, true, true);
		}

		private void ListsPromptsPanel_AddPromptButton_Click(object sender, RoutedEventArgs e)
		{
			var promptItem = Global.Lists.Items.FirstOrDefault(x => x.Name == _Item?.ListPromptName);
			if (promptItem == null)
				return;
			var promptOption = promptItem.Items.FirstOrDefault(x => x.Key == _Item?.ListPromptOption);
			if (promptOption == null)
				return;
			var box = ChatPanel.GetFocusedTextBox();
			AppHelper.InsertText(box, promptOption.Value, true, true);
		}

		bool WebBrowserDataLoaded;

		private async void ChatPanel_MessagesPanel_WebBrowserDataLoaded(object sender, EventArgs e)
		{
			await Helper.Debounce(SetZoom, AppHelper.NavigateDelayMs);
			WebBrowserDataLoaded = true;
			RestoreTabSelection();
		}

		private async void ChatPanel_MessagesPanel_ScriptingHandler_OnMessageAction(object sender, string[] e)
		{
			var actionString = e[1];
			if (string.IsNullOrEmpty(actionString))
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), actionString);
			var ids = (e[0] ?? "").Split('_');
			var messageId = ids[0];
			var message = ChatPanel.MessagesPanel.Messages.FirstOrDefault(x => x.Id == messageId);
			if (message == null)
				return;
			if (action == MessageAction.Use)
			{
				ChatPanel.DataTextBox.PART_ContentTextBox.Text = message.Body;
				ChatPanel.EditMessageId = null;
				ChatPanel.EditAttachmentId = null;
				ChatPanel.FocusChatInputTextBox();
			}
			else if (action == MessageAction.Regenerate)
			{
				ChatPanel.EditMessageId = messageId;
				ChatPanel.FocusChatInputTextBox();
				var voiceInstructions = GetVoiceInstructions();
				await ClientHelper.Send(_Item, ChatPanel.ApplyMessageEditWithRemovingMessages, message.Body, extraInstructions: voiceInstructions);
			}
			else if (action == MessageAction.Edit)
			{
				ChatPanel.DataTextBox.PART_ContentTextBox.Text = message.Body;
				ChatPanel.EditMessageId = messageId;
				ChatPanel.FocusChatInputTextBox();
			}
			else if (action == MessageAction.EditAttachment)
			{
				var attachmentId = ids[1];
				ChatPanel.EditMessageId = messageId;
				ChatPanel.EditAttachmentId = attachmentId;
				ChatPanel.MaskDrawingTabItem.Visibility = Visibility.Visible;
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
			UpdateSpellCheckForTextBox(ChatPanel.DataTextBox.PART_ContentTextBox, isEnabled);
			UpdateSpellCheckForTextBox(ChatPanel.DataInstructionsTextBox.PART_ContentTextBox, isEnabled);
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
			if (box == ChatPanel.DataTextBox.PART_ContentTextBox)
				await Helper.Debounce(EnableOnDataTextBox);
			if (box == ChatPanel.DataInstructionsTextBox.PART_ContentTextBox)
				await Helper.Debounce(EnableOnDataInstructionsTextBox);
		}

		void EnableOnDataTextBox()
			=> ChatPanel.DataTextBox.PART_ContentTextBox.SpellCheck.IsEnabled = true;
		void EnableOnDataInstructionsTextBox()
			=> ChatPanel.DataInstructionsTextBox.PART_ContentTextBox.SpellCheck.IsEnabled = true;

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
			if (_Item == null)
				return;
			if (ChatPanel.IsChatInputExpanded)
				// Make panel normal again, so that the user can see the chat log.
				ChatPanel.MaximizeAndNormal();

			var voiceInstructions = GetVoiceInstructions();
			var isCtrlDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
			var isAltDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt);
			message_role? addMessageAsRole = null;
			if (isCtrlDown)
				addMessageAsRole = message_role.user;
			if (isAltDown)
				addMessageAsRole = message_role.assistant;
			var isEdit = !string.IsNullOrEmpty(ChatPanel.EditMessageId);
			if (isEdit)
			{
				ChatPanel.ApplyMessageEdit();
			}
			else
			{
				await ClientHelper.Send(_Item, ChatPanel.ApplyMessageEditWithRemovingMessages,
					extraInstructions: voiceInstructions,
					addMessageAsRole: addMessageAsRole);
			}
			RestoreTabSelection();
		}

		string GetVoiceInstructions()
		{
			// If use voice is checked or if avatar is visible.
			var useVoice = Item.UseAvatarVoice || Global.IsAvatarInWindow || Item.ShowAvatar;
			if (useVoice)
				return Global.AppSettings.AiAvatar.Instructions;
			return null;
		}

		private void ChatPanel_OnStop(object sender, EventArgs e)
		{
			_Item?.StopClients();
			RestoreTabSelection();
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

		public Dictionary<string, string> GenerateTitleTemplates
		{
			get
			{
				var kv = Global.Templates.Items
					.Where(x => x.Name.StartsWith(SettingsSourceManager.TemplateGenerateTitleTaskName))
					.ToDictionary(x => x.Name, x => x.Name);
				//kv.Add(null, "Default");
				return kv;
			}
			set { }
		}

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

		public ObservableCollection<CheckBoxViewModel> AttachContexts
		{
			get
			{
				if (_AttachContexts == null)
					_AttachContexts = EnumComboBox.GetItemSource<ContextType>();
				return _AttachContexts;
			}
			set => _AttachContexts = value;
		}
		ObservableCollection<CheckBoxViewModel> _AttachContexts;

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
				ChatPanel.MonitorTextBoxSelections(false);
				// Make sure that custom AiModel old and new item is available to select.
				AppHelper.UpdateModelCodes(value?.AiService, AiModelBoxPanel.AiModels, value?.AiModel, oldItem?.AiModel);
				// Set new item.
				_Item = value ?? AppHelper.GetNewTemplateItem(true);
				// This will trigger AiCompanionComboBox_SelectionChanged event.
				AiModelBoxPanel.Item = null;
				if (ChatPanel.AttachmentsPanel.CurrentItems != null)
					ChatPanel.AttachmentsPanel.CurrentItems = null;
				DataContext = _Item;
				_Item.PropertyChanged += _item_PropertyChanged;
				AiModelBoxPanel.Item = _Item;
				ToolsPanel.Item = _Item;
				OnPropertyChanged(nameof(CreativityName));
				// New item is bound. Make sure that custom AiModel only for the new item is available to select.
				AppHelper.UpdateModelCodes(_Item.AiService, AiModelBoxPanel.AiModels, _Item?.AiModel);
				PluginApprovalPanel.Item = _Item.PluginFunctionCalls;
				ChatPanel.AttachmentsPanel.CurrentItems = _Item.Attachments;
				IconPanel.BindData(_Item);
				CanvasPanel.Item = _Item;
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
						_ = ClientHelper.Send(_Item, ChatPanel.ApplyMessageEditWithRemovingMessages, extraInstructions: voiceInstructions);
					});
				}
				_ = Helper.Debounce(EmbeddingGroupFlags_OnPropertyChanged);
				if (PanelSettings.Focus)
					RestoreTabSelection();
				ChatPanel.MonitorTextBoxSelections(true);
				UpdateAvatarControl();
				UpdateListEditButtons();
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
					if (_Item.UseSystemInstructions && text.Contains(TextToProcess))
					{
						var s = text.Replace(TextToProcess, "").TrimEnd();
						AppHelper.SetText(ChatPanel.DataInstructionsTextBox.PART_ContentTextBox, s);
					}
					else if (!_Item.UseSystemInstructions && !containsDataHeader && !string.IsNullOrEmpty(text))
					{
						var s = ClientHelper.JoinMessageParts(text, TextToProcess);
						AppHelper.SetText(ChatPanel.DataInstructionsTextBox.PART_ContentTextBox, s);
					}
					break;
				case nameof(TemplateItem.ShowAvatar):
					UpdateAvatarControl();
					break;
				case nameof(TemplateItem.EmbeddingGroupName):
					_ = Helper.Debounce(EmbeddingGroupFlags_OnPropertyChanged);
					break;
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

				var binding = new System.Windows.Data.Binding();
				binding.Path = new PropertyPath(nameof(TemplateItemVisibility));
				binding.Source = this;
				ChatPanel.MessagePlaceholderTabItem.SetBinding(UIElement.VisibilityProperty, binding);


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
			var box = ChatPanel.GetFocusedTextBox();
			AppHelper.InsertText(box, "{" + item.Key + "}");
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
				CodeBlockPanel.GetFocused = ChatPanel.GetFocusedTextBox;
				AppHelper.InitHelp(this);
				// Remove control, which visibility is controlled by the code.
				var excludeElements = new FrameworkElement[] {
					this, MainTabControl,
					ZoomSlider,
					// Plugin approval.
					ColumnWidthBorder,
					PluginsEnableContextCheckBox,
					MaximumRiskLevelComboBox,
					PluginApprovalProcessComboBox,
					PluginApprovalTemplateComboBox,
					// Other controls.
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
			RestoreTabSelection();
			UpdateAvatarControl();
			// Workaround after resetting settings.
			if (RebindItemOnLoad)
			{
				RebindItemOnLoad = false;
				var item = Item;
				Item = null;
				if (item != null)
					_ = Helper.Debounce(() => Item = item);
			}
		}

		public bool RebindItemOnLoad = false;

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
			RestoreTabSelection();
		}

		private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
		{
			ChatPanel.MessagesPanel.InvokeScript("ScrollToBottom()");
			RestoreTabSelection();
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

		private void ChatPanel_SelectionSaved(object sender, EventArgs e)
		{
			var tab = ChatPanel.SelectionControls.FirstOrDefault(x => x.Tab.IsSelected).Tab;
			if (tab != null)
				PanelSettings.FocusedControl = tab.Name;
		}

		private void RestoreTabSelection()
		{
			// Note: Setting focus during web browser loading fails to hide textbox placeholder.
			if (WebBrowserDataLoaded)
				_ = Helper.Debounce(_RestoreTabSelection, AppHelper.NavigateDelayMs);
		}

		private void _RestoreTabSelection()
		{
			var tab = ChatPanel.SelectionControls.FirstOrDefault(x => x.Tab.Name == PanelSettings.FocusedControl).Tab;
			if (tab != null)
				tab.IsSelected = true;
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
				await Helper.Debounce(SetZoom);
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

		public ObservableCollection<CheckBoxViewModel> EmbeddingGroupFlags
		{
			get
			{
				if (_EmbeddingGroupFlags == null)
					_EmbeddingGroupFlags = EnumComboBox.GetItemSource<EmbeddingGroupFlag>();
				EmbeddingHelper.UpdateGroupFlagsFromDatabase(Item?.UseEmbeddings == true ? Item?.EmbeddingName : null, _EmbeddingGroupFlags);
				return _EmbeddingGroupFlags;
			}
			set => _EmbeddingGroupFlags = value;
		}
		ObservableCollection<CheckBoxViewModel> _EmbeddingGroupFlags;

		private void Embeddings_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			var update = false;
			if (e.ListChangedType == ListChangedType.ItemChanged && e.PropertyDescriptor?.Name == nameof(TemplateItem.EmbeddingName))
				update = true;
			if (e.ListChangedType == ListChangedType.ItemAdded ||
				e.ListChangedType == ListChangedType.ItemDeleted)
				update = true;
			if (update)
				_ = Helper.Debounce(UpdateEmbeddingNames);
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
				_ = Helper.Debounce(UpdateMailAccounts);
		}

		#endregion

		private void AttachmentsButton_Click(object sender, RoutedEventArgs e)
		{
			//ChatPanel.AttachmentsPanel.AddFile();
			var files = ChatPanel.AttachmentsPanel.GetFiles();
			if (files == null || !files.Any())
				return;
			var textBox = ChatPanel.GetFocusedTextBox();
			AppControlsHelper.DropFiles(textBox, files);
		}

		private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
		{
			var path = System.IO.Path.Combine(AppHelper.GetTempFolderPath(), "Screenshots");
			var isCtrlDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
			if (isCtrlDown && !Global.IsVsExtension)
				Global.TrayManager.MinimizeToTray(false, Global.AppSettings.MinimizeToTray);
			var captureResult = await ScreenshotHelper.CaptureRegion(null, path, System.Drawing.Imaging.ImageFormat.Jpeg);
			if (captureResult.Success)
			{
				var box = ChatPanel.GetFocusedTextBox();
				AppHelper.InsertText(box, $"Please analyse screenshot\r\n{captureResult.Data}", true, false);
			}
			if (isCtrlDown && !Global.IsVsExtension)
				Global.TrayManager.RestoreFromTray(true, false);
		}


		private void MicrophoneButton_Click(object sender, RoutedEventArgs e)
		{
			var box = ChatPanel.GetFocusedTextBox();
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
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Export HTML File";
			dialog.Filter = "Webpage, single file (*.html)|*.html";
			//DialogHelper.AddFilter(dialog, ".pdf");
			//DialogHelper.AddFilter(dialog, ".rtf");
			DialogHelper.AddFilter(dialog);
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			// Cast the document to an HTMLDocument
			var html = GetPageHtml();
			if (string.IsNullOrEmpty(html))
				return;
			var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
			switch (ext)
			{
				case ".pdf":
					var pdf = PdfGenerator.GeneratePdf(html, PdfSharp.PageSize.A4);
					pdf.Save(dialog.FileName);
					break;
				default:
					var bytes = System.Text.Encoding.UTF8.GetBytes(html);
					SettingsHelper.WriteIfDifferent(dialog.FileName, bytes);
					break;
			}
		}

		public string GetPageHtml()
		{
			// Cast the document to an HTMLDocument
			var html = (string)ChatPanel.MessagesPanel.InvokeScript("document.documentElement.outerHTML;");
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

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
