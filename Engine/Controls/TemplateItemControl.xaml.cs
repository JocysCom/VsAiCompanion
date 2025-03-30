using DocumentFormat.OpenXml.Drawing.Diagrams;
using HtmlAgilityPack;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Processes;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
			ChatPanel.MessagesPanel.WebBrowserHostObject.OnMessageAction += ChatPanel_MessagesPanel_WebBrowserHostObject_OnMessageAction;
			ChatPanel.SelectionSaved += ChatPanel_SelectionSaved;
			ChatPanel.PropertyChanged += ChatPanel_PropertyChanged;
			ChatPanel.MainTabControl.SelectionChanged += ChatPanel_MainTabControl_SelectionChanged;
			ChatPanel.InitTokenCounters();
			//SolutionRadioButton.IsEnabled = Global.GetSolutionDocuments != null;
			//ProjectRadioButton.IsEnabled = Global.GetProjectDocuments != null;
			//FileRadioButton.IsEnabled = Global.GetSelectedDocuments != null;
			//SelectionRadioButton.IsEnabled = Global.GetSelection != null;
			Global.OnSaveSettings += Global_OnSaveSettings;
			PromptsPanel.AddPromptButton.Click += PromptsPanel_AddPromptButton_Click;
			ListsPromptsPanel.AddPromptButton.Click += ListsPromptsPanel_AddPromptButton_Click;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			UpdateSpellCheck();
			var checkBoxes = ControlsHelper.GetAll<CheckBox>(this);
			AppHelper.EnableKeepFocusOnMouseClick(checkBoxes);
			Global.OnTabControlSelectionChanged += Global_OnTabControlSelectionChanged;
			Global.Templates.Items.ListChanged += Global_Templates_Items_ListChanged;
		}

		public Dictionary<string, string> PluginTemplates
			=> Global.Templates.Items.ToDictionary(x => x.Name, x => x.Name);

		private void Global_Templates_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, () =>
			{
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
			UpdateMonotypeFont();
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

		private async void ChatPanel_MessagesPanel_WebBrowserHostObject_OnMessageAction(object sender, (string id, string action, string data) e)
		{
			if (string.IsNullOrEmpty(e.action))
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), e.action);
			var ids = (e.id ?? "").Split('_');
			var messageId = ids[0];
			var message = ChatPanel.MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == messageId);
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
			var attachmentActions = new MessageAction[] {
				MessageAction.OpenFile,
				MessageAction.EditFile,
				MessageAction.ExploreFile
			};
			if (attachmentActions.Contains(action))
			{
				var attachmentId = ids[1];
				var attachment = message.Attachments?.FirstOrDefault(x => x.Id == attachmentId);
				if (attachment == null)
					return;
				BasicInfo info = null;
				string fileFullPath = null;
				if (attachment.Type == ContextType.Image || attachment.Type == ContextType.Audio)
				{
					info = Client.Deserialize<BasicInfo>(attachment.Data);
					var folderPath = Global.GetPath(Item);
					fileFullPath = Path.Combine(folderPath, info.Name);
				}
				if (action == MessageAction.EditAttachment)
				{
					ChatPanel.EditMessageId = messageId;
					ChatPanel.EditAttachmentId = attachmentId;
					ChatPanel.MaskDrawingTabItem.Visibility = Visibility.Visible;
				}
				else if (action == MessageAction.OpenFile)
				{
					if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
						FileExplorerHelper.OpenWithFile(fileFullPath);
					else
						FileExplorerHelper.OpenFile(fileFullPath);
				}
				else if (action == MessageAction.EditFile)
				{
					FileExplorerHelper.EditFile(fileFullPath);
				}
				else if (action == MessageAction.ExploreFile)
				{
					FileExplorerHelper.OpenFileInExplorerAndSelect(fileFullPath);
				}
			}
		}

		private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
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

		private async void Global_OnSaveSettings(object sender, EventArgs e)
		{
			// Update from previous settings.
			if (_Item != null)
			{
				var settings = await ChatPanel.MessagesPanel.GetWebSettingsAsync();
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
				await ChatPanel.ApplyMessageEditAsync();
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

		public Dictionary<ToolCallApprovalProcess, string> PluginApprovalProcesses
				=> ClassLibrary.Runtime.Attributes.GetDictionary((ToolCallApprovalProcess[])Enum.GetValues(typeof(ToolCallApprovalProcess)));

		public Dictionary<RiskLevel, string> MaxRiskLevels
			=> ClassLibrary.Runtime.Attributes.GetDictionary(
				((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new[] { RiskLevel.Unknown }).ToArray());

		#region ■ Properties

		public TemplateItem Item
		{
			get => _Item;
		}
		TemplateItem _Item;

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
			}
		}
		private ItemType _DataType;

		public async Task BindData(TemplateItem item)
		{
			if (Equals(item, _Item))
				return;
			var oldItem = _Item;
			// Update from previous settings.
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
				var settings = await ChatPanel.MessagesPanel.GetWebSettingsAsync();
				_Item.Settings = settings;
			}
			ChatPanel.MonitorTextBoxSelections(false);
			// Make sure that custom AiModel old and new item is available to select.
			AppHelper.UpdateModelCodes(item?.AiService, AiModelBoxPanel.AiModels, item?.AiModel, oldItem?.AiModel);
			// Set new item.
			_Item = item ?? AppHelper.GetNewTemplateItem(true);
			// This will trigger AiCompanionComboBox_SelectionChanged event.
			AiModelBoxPanel.Item = null;
			if (ChatPanel.AttachmentsPanel.CurrentItems != null)
				ChatPanel.AttachmentsPanel.CurrentItems = null;
			DataContext = _Item;
			await MainPanel.BindData(_Item, DataType, ChatPanel);
			await PersonalizationPanel.BindData(_Item);
			await VisualStudioPanel.BindData(_Item);
			await EmbeddingsPanel.BindData(_Item);
			await MailPanel.BindData(_Item);
			await RequestDataPanel.BindData(_Item);
			_Item.PropertyChanged += _item_PropertyChanged;
			AiModelBoxPanel.Item = _Item;
			ToolsPanel.Item = _Item;
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			AppHelper.UpdateModelCodes(_Item.AiService, AiModelBoxPanel.AiModels, _Item?.AiModel);
			PluginApprovalPanel.Item = _Item.PluginFunctionCalls;
			ChatPanel.AttachmentsPanel.CurrentItems = _Item.Attachments;
			CanvasPanel.Item = _Item;
			ExternalModelsPanel.Item = _Item;
			PromptsPanel.BindData(_Item);
			ListsPromptsPanel.BindData(_Item);
			ChatPanel.MessagesPanel.SetDataItems(_Item);
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
			if (PanelSettings.Focus)
				RestoreTabSelection();
			ChatPanel.MonitorTextBoxSelections(true);
			UpdateAvatarControl();
			UpdateMonotypeFont();
		}

		#endregion

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
				case nameof(TemplateItem.UseMonotypeFont):
					UpdateMonotypeFont();
					break;
				default:
					break;
			}
		}

		public void UpdateAvatarControl()
		{
			Global.UpdateAvatarControl(ChatPanel.AvatarPanelBorder, Item?.ShowAvatar == true);
		}

		public void UpdateMonotypeFont()
		{
			ChatPanel.EnableMonotypeFont(Item?.UseMonotypeFont == true);
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				CodeBlockPanel.GetFocused = ChatPanel.GetFocusedTextBox;
				VisualStudioPanel.GetFocused = ChatPanel.GetFocusedTextBox;
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
 				};
				UiPresetsManager.InitControl(this, excludeElements: excludeElements);
			}
			RestoreTabSelection();
			UpdateAvatarControl();
			UpdateMonotypeFont();
			// Workaround after resetting settings.
			if (RebindItemOnLoad)
			{
				RebindItemOnLoad = false;
				var item = Item;
				await BindData(null);
				if (item != null)
					_ = Helper.Debounce(async () => await BindData(item));
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
			ChatPanel.MessagesPanel.SetDataItems(_Item);
			ChatPanel.UpdateMessageEdit();
			RestoreTabSelection();
		}

		private async void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
		{
			await ChatPanel.MessagesPanel.InvokeScriptAsync("ScrollToBottom()");
			RestoreTabSelection();
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

		private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
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
			var html = await GetPageHtmlAsync();
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

		public async Task<string> GetPageHtmlAsync()
		{
			// Cast the document to an HTMLDocument
			var html = await ChatPanel.MessagesPanel.InvokeScriptAsync("return document.documentElement.outerHTML;") as string;
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

		private void ExploreButton_Click(object sender, RoutedEventArgs e)
		{
			var fileFullPath = Global.GetPath(Item) + ".xml";
			FileExplorerHelper.OpenFileInExplorerAndSelect(fileFullPath);
		}

		private async void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			var html = await GetPageHtmlAsync();
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
