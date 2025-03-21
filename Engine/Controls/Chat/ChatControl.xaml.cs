﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Resources;
using JocysCom.VS.AiCompanion.Engine.Resources.Icons;
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
using System.Windows.Media;
using System.Windows.Threading;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	/// <summary>
	/// Interaction logic for ChatControl.xaml
	/// </summary>
	public partial class ChatControl : UserControl, INotifyPropertyChanged
	{
		public ChatControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			InitControlsAllowedToRememer();
			UpdateControlButtons();
			UpdateMessageEdit();
			foreach (var item in SelectionControls)
			{
				AppControlsHelper.AllowDrop(item.Box, true);
				AppControlsHelper.AllowPasteFiles(item.Box, true);
			}
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			UpdateEnterToSend();
		}

		private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AppData.UseEnterToSendMessage):
					UpdateEnterToSend();
					break;
				default:
					break;
			}
		}

		void UpdateEnterToSend()
		{
			var action = Global.AppSettings.UseEnterToSendMessage ? new Func<bool>(Send) : null;
			AppControlsHelper.UseEnterToSend(DataTextBox.PART_ContentTextBox, action);
		}

		public string EditAttachmentId
		{
			get { return _EditAttachmentId; }
			set { _EditAttachmentId = value; UpdateMessageEdit(); }
		}
		string _EditAttachmentId;

		public string EditMessageId
		{
			get { return _EditMessageId; }
			set { _EditMessageId = value; UpdateMessageEdit(); }
		}
		string _EditMessageId;

		// It set to true then regenerate action will be performent after applying editing of the message.
		bool regenerateMessageActionAfterEdit = false;

		private void Tasks_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			UpdateControlButtons();
		}

		public string Connect = "Connect";
		public string Disconnect = "Disconnect";
		public bool IsConnected;
		public bool SendPublicKey;

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				//var boxes = new PlaceholderTextBox[] { RisenRoleTextBox, RisenInstructionsTextBox, RisenStepsTextBox, RisenEndGoalTextBox, RisenNarrowingTextBox };
				//foreach (var box in boxes)
				//{
				//	box.PART_ContentTextBox.BorderThickness = new Thickness(0);
				//	box.PART_ContentTextBox.Margin = new Thickness(3);
				//	box.PART_ContentTextBox.Padding = new Thickness(10, 7, 10, 7);
				//	box.PART_PlaceholderTextBox.BorderThickness = new Thickness(0);
				//	box.PART_PlaceholderTextBox.Margin = new Thickness(3);
				//	box.PART_PlaceholderTextBox.Padding = new Thickness(14, 7, 10, 7);
				//}
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true,
					new FrameworkElement[] {
						RisenRoleTextBox, RisenInstructionsTextBox, RisenStepsTextBox, RisenEndGoalTextBox, RisenNarrowingTextBox,
						RisenRoleTabItem, RisenInstructionsTabItem, RisenStepsTabItem, RisenEndGoalTabItem, RisenNarrowingTabItem,
					});
			}
		}

		const string SendingMessage = nameof(SendingMessage);

		public void ShowMessage(string message)
		{
			ControlsHelper.Invoke(() =>
			{
				var box = new MessageBoxWindow();
				box.SetSize(800, 600);
				box.ShowPrompt(message, "XML Text", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
			});
		}

		/// <summary>
		/// Determines if the current message can be sent based on content validation.
		/// </summary>
		/// <returns>True if the message can be sent; otherwise, false.</returns>
		public bool AllowToSend()
		{
			return !string.IsNullOrEmpty(DataTextBox.PART_ContentTextBox.Text);
		}

		public bool Send()
		{
			if (AllowToSend())
				OnSend?.Invoke(this, EventArgs.Empty);
			return true;
		}

		private void DataInstructionsTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
		{
			UpdateMessageEdit();
		}

		private void DataInstructionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateMessageEdit();
		}

		private void DataTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateMessageEdit();
		}

		/// <summary>
		/// Schedules an update of the message edit UI with debouncing to prevent excessive updates.
		/// </summary>
		public void UpdateMessageEdit()
		{
			_ = Helper.Debounce(UpdateMessageEditDebounced, 100);
		}

		public bool IsBusy;

		/// <summary>
		/// Determines if messages after the specified index contain only errors or empty messages.
		/// </summary>
		/// <returns>True if all subsequent messages are errors or empty; otherwise, false.</returns>
		public bool DoRegenerateMessageActionAfterEdit()
		{
			if (string.IsNullOrEmpty(EditMessageId))
				return false;
			var editMessage = MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == EditMessageId);
			if (editMessage == null)
				return false;
			// Don't regenerate if not user message.
			if (editMessage.Type != MessageType.Out)
				return false;
			var messageIndex = MessagesPanel.Item.Messages.IndexOf(editMessage);
			if (messageIndex < 0 || MessagesPanel.Item?.Messages == null || messageIndex >= MessagesPanel.Item.Messages.Count - 1)
				return false;
			for (int i = messageIndex + 1; i < MessagesPanel.Item.Messages.Count; i++)
			{
				var message = MessagesPanel.Item.Messages[i];
				// Consider a message non-empty and non-error if it's from assistant and has content
				if (message.Type == MessageType.In && !string.IsNullOrWhiteSpace(message.Body))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Updates the message edit UI components based on current edit state and determines if regeneration is needed.
		/// </summary>
		public void UpdateMessageEditDebounced()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			var isAttachmentEdit = !string.IsNullOrEmpty(EditAttachmentId);
			AppHelper.UpdateHelp(SendButton,
				isEdit ? MainResources.main_Chat_Apply_Name : MainResources.main_Chat_Send_Name,
				isEdit ? MainResources.main_Chat_Apply_Help : MainResources.main_Chat_Send_Help);
			AppHelper.UpdateHelp(StopButton,
				isEdit ? MainResources.main_Chat_Cancel_Name : MainResources.main_Chat_Stop_Name,
				isEdit ? MainResources.main_Chat_Cancel_Help : MainResources.main_Chat_Stop_Help);
			// Select message tab if options are invisible but selected.
			if (!isEdit && MessageOptionsTabItem.IsSelected)
				ChatMessageTabItem.IsSelected = true;
			MessageOptionsTabItem.Visibility = isEdit && !isAttachmentEdit ? Visibility.Visible : Visibility.Collapsed;
			MaskDrawingTabItem.Visibility = isAttachmentEdit ? Visibility.Visible : Visibility.Collapsed;
			MessageOptionsPanel.DataContext = isEdit ? MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == EditMessageId) : null;

			// Determine if regeneration is needed after edit
			regenerateMessageActionAfterEdit = DoRegenerateMessageActionAfterEdit();

			SendButtonIcon.Content = isEdit
				? Resources[regenerateMessageActionAfterEdit ? Icons_Default.Icon_button_ok_refresh : Icons_Default.Icon_button_ok]
				: Resources[Icons_Default.Icon_media_play];
			StopButtonIcon.Content = isEdit
				? Resources[Icons_Default.Icon_button_cancel]
				: Resources[Icons_Default.Icon_media_stop];
			SendButton.IsEnabled = !IsBusy && AllowToSend();
			StopButton.IsEnabled = isEdit || IsBusy;
			var sendOp = SendButton.IsEnabled ? 1.0 : 0.2;
			if (SendButton.Opacity != sendOp)
				SendButton.Opacity = sendOp;
			var stopOp = StopButton.IsEnabled ? 1.0 : 0.2;
			if (StopButton.Opacity != stopOp)
				StopButton.Opacity = stopOp;
			System.Diagnostics.Debug.WriteLine($"UpdateButtons: IsBusy={IsBusy}, isEdit={isEdit}, stopOp={stopOp}");
			// Bind attachments.
			var attachments = isEdit ? MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == EditMessageId)?.Attachments : null;
			if (AttachmentsPanel.CurrentItems != attachments)
			{
				AttachmentsPanel.CurrentItems = attachments;
				AttachmentsPanel.CurrentItems2.Add(new MessageAttachments() { Title = "External " });
			}
		}

		void UpdateControlButtons()
		{
			ControlsHelper.Invoke(() =>
			{
				//var isBusy = InfoPanel.Tasks.Count > 0;
				//ControlsHelper.SetEnabled(ShowProgramXmlButton, _SelectedWindowMsaaItem != null);
				//ControlsHelper.SetEnabled(ShowChatXmlButton, _SelectedWindowMsaaItem != null);
			});
		}

		public event EventHandler OnSend;
		public event EventHandler OnStop;

		/// <summary>
		/// Applies changes to an edited message and clears the edit state.
		/// </summary>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task ApplyMessageEditAsync()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			if (isEdit)
			{
				var message = MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == EditMessageId);
				if (message != null)
				{
					message.Body = DataTextBox.PART_ContentTextBox.Text;
					message.BodyInstructions = DataInstructionsTextBox.PART_ContentTextBox.Text;
					DataTextBox.PART_ContentTextBox.Text = "";
					await MessagesPanel.UpdateWebMessage(message, true);

					// If regeneration is needed after edit, perform it
					if (regenerateMessageActionAfterEdit)
					{
						// Save the message ID before clearing edit state
						var messageId = EditMessageId;
						EditMessageId = null;
						EditAttachmentId = null;

						// Find the message again (since we just cleared the edit state)
						var messageToRegenerate = MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == messageId);
						if (messageToRegenerate != null)
						{
							// Trigger regeneration by removing subsequent messages and sending
							var messageIndex = MessagesPanel.Item.Messages.IndexOf(messageToRegenerate);
							if (messageIndex > -1)
							{
								var messagesToDelete = MessagesPanel.Item.Messages.Skip(messageIndex + 1).ToArray();
								foreach (var messageToDelete in messagesToDelete)
									await MessagesPanel.RemoveMessageAsync(messageToDelete);

								// Notify that a message action needs to be performed
								OnSend?.Invoke(this, EventArgs.Empty);
							}
						}
						return;
					}
				}
			}
			EditMessageId = null;
			EditAttachmentId = null;
		}

		/// <summary>
		/// If the chat form is in message edit mode, remove relevant messages before adding a new message.
		/// </summary>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task ApplyMessageEditWithRemovingMessages()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			if (isEdit)
			{
				var message = MessagesPanel.Item.Messages.FirstOrDefault(x => x.Id == EditMessageId);
				if (message != null)
				{
					var messageIndex = MessagesPanel.Item.Messages.IndexOf(message);
					if (messageIndex > -1)
					{
						var messagesToDelete = MessagesPanel.Item.Messages.Skip(messageIndex).ToArray();
						foreach (var messageToDelete in messagesToDelete)
							await MessagesPanel.RemoveMessageAsync(messageToDelete);
					}
				}
			}
			EditMessageId = null;
			EditAttachmentId = null;
		}

		private void SendButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			OnSend?.Invoke(sender, e);
		}

		private void This_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			UpdateMaxSize();
		}

		private void DataInstructionsPanel_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			UpdateMaxSize();
		}

		double? MessageMaxHeightOverride;

		private void UpdateMaxSize()
		{
			var maxHeight = Math.Round(ActualHeight * (MessageMaxHeightOverride ?? 0.4));
			foreach (var item in SelectionControls)
				item.Box.MaxHeight = maxHeight;
		}

		private void StopButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			if (isEdit)
			{
				DataTextBox.PART_ContentTextBox.Text = "";
				EditMessageId = null;
				EditAttachmentId = null;
				UpdateMessageEdit();
			}
			else
			{
				OnStop?.Invoke(sender, e);
			}
		}

		private void AttachmentsButton_Click(object sender, RoutedEventArgs e)
		{

		}

		#region RISEN Framework

		/// <summary>
		/// Updates a set of text boxes to use either a monospaced font or the system default font.
		/// </summary>
		/// <param name="enable">
		/// If true, text boxes will use the monospaced font "Consolas"; otherwise, they revert to the system default font.
		/// </param>
		public void EnableMonotypeFont(bool enable)
		{
			// Define the fonts.
			FontFamily monotypeFont = new FontFamily("Consolas");
			FontFamily defaultFont = SystemFonts.MessageFontFamily;
			FontFamily targetFont = enable ? monotypeFont : defaultFont;
			// Array of text boxes to update.
			TextBox[] boxes = new[]
			{
				DataTextBox.PART_ContentTextBox,
				DataInstructionsTextBox.PART_ContentTextBox,
				RisenRoleTextBox.PART_ContentTextBox,
				RisenInstructionsTextBox.PART_ContentTextBox,
				RisenStepsTextBox.PART_ContentTextBox,
				RisenEndGoalTextBox.PART_ContentTextBox,
				RisenNarrowingTextBox.PART_ContentTextBox,
			};
			foreach (TextBox box in boxes)
			{
				if (box == null)
					continue;
				// Only update if the current font is different.
				if (!string.Equals(box.FontFamily?.Source, targetFont.Source, StringComparison.OrdinalIgnoreCase))
					// Use SetCurrentValue to update without interfering with bindings.
					box.SetCurrentValue(Control.FontFamilyProperty, targetFont);
			}
		}

		void EnableRisen(bool enable)
		{
			var boxes = new[]{
				RisenRoleTextBox.PART_ContentTextBox,
				RisenInstructionsTextBox.PART_ContentTextBox,
				RisenStepsTextBox.PART_ContentTextBox,
				RisenEndGoalTextBox.PART_ContentTextBox,
				RisenNarrowingTextBox.PART_ContentTextBox,
			};
			if (enable)
			{
				UpdateRisenFromMessage();
			}
			else
			{
				if (!ChatMessageTabItem.IsSelected || !ChatInstructionsTabItem.IsSelected)
					ChatMessageTabItem.IsSelected = true;
			}
			foreach (var box in boxes)
			{
				if (enable)
					box.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(RisenBox_TextChanged), handledEventsToo: true);
				else
					box.RemoveHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(RisenBox_TextChanged));
			}
		}

		private async void RisenBox_TextChanged(object sender, TextChangedEventArgs e)
				=> await Helper.Debounce(UpdateMessageFromRisen);

		void UpdateMessageFromRisen()
		{
			var item = Item;
			if (item is null)
				return;
			if (!item.ShowRisen)
				return;
			var text = RisenHelper.ConstructPrompt(
				RisenRoleTextBox.Text,
				RisenInstructionsTextBox.Text,
				RisenStepsTextBox.Text,
				RisenEndGoalTextBox.Text,
				RisenNarrowingTextBox.Text);
			ControlsHelper.SetText(DataTextBox.PART_ContentTextBox, text);
		}

		void UpdateRisenFromMessage()
		{
			var item = Item;
			if (item is null)
				return;
			var result = RisenHelper.ExtractProperties(item.Text);
			if (result == null)
				return;
			RisenRoleTextBox.Text = result.Value.Role;
			RisenInstructionsTextBox.Text = result.Value.Instructions;
			RisenStepsTextBox.Text = result.Value.Steps;
			RisenEndGoalTextBox.Text = result.Value.EndGoal;
			RisenNarrowingTextBox.Text = result.Value.Narrowing;
		}

		#endregion

		#region Load and Save Selections

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var tab = MainTabControl.SelectedItem as TabItem;
			if (tab == null)
				return;
			var contentTextBox = SelectionControls?.FirstOrDefault(x => x.Tab == tab).Box;
			if (contentTextBox == null)
				return;
			Dispatcher.BeginInvoke(new Action(() =>
			{
				LoadSelection(contentTextBox);
			}), DispatcherPriority.Render);
		}

		public void MonitorTextBoxSelections(bool enable)
		{
			foreach (var item in SelectionControls)
			{
				if (enable)
					item.Box.AddHandler(TextBox.SelectionChangedEvent, new RoutedEventHandler(Box_SelectionChanged), handledEventsToo: true);
				else
					item.Box.RemoveHandler(TextBox.SelectionChangedEvent, new RoutedEventHandler(Box_SelectionChanged));
			}
		}

		public event EventHandler SelectionSaved;

		private void Box_SelectionChanged(object sender, RoutedEventArgs e)
		{
			var box = (TextBox)sender;
			if (!box.IsFocused)
				return;
			var group = SelectionControls.FirstOrDefault(x => x.Box == box);
			// Save selection only if tab is visible.
			if (((TabControl)group.Tab.Parent).SelectedItem == group.Tab)
			{
				SaveSelection(box);
				SelectionSaved?.Invoke(this, EventArgs.Empty);
			}
		}

		public TabItem GetSelectedTab()
			=> SelectionControls.FirstOrDefault(x => x.Tab == MainTabControl.SelectedItem).Tab;

		public TextBox GetFocusedTextBox()
			=> SelectionControls.FirstOrDefault(x => x.Tab == MainTabControl.SelectedItem).Box;

		public void FocusChatInputTextBox()
		{
			var box = GetFocusedTextBox();
			if (box is null)
				return;
			LoadSelection(box);
		}

		/// <summary>
		/// Get textboxes which can store selection data.
		/// Other data must be removed.
		/// </summary>
		void InitControlsAllowedToRememer()
		{
			SelectionControls = new (TabItem, PlaceholderTextBox, TextBox, Label, RisenType)[] {
				(ChatInstructionsTabItem, DataInstructionsTextBox, DataInstructionsTextBox.PART_ContentTextBox, InstructionsCountLabel, RisenType.None),
				(ChatMessageTabItem, DataTextBox, DataTextBox.PART_ContentTextBox, MessageCountLabel, RisenType.None),
				(MessagePlaceholderTabItem, MessagePlaceholderTextBox, MessagePlaceholderTextBox.PART_ContentTextBox, MessagePlaceholderCountLabel, RisenType.None),
				(RisenRoleTabItem, RisenRoleTextBox, RisenRoleTextBox.PART_ContentTextBox, RisenRoleCountLabel, RisenType.Role),
				(RisenInstructionsTabItem, RisenInstructionsTextBox, RisenInstructionsTextBox.PART_ContentTextBox, RisenInstructionsCountLabel, RisenType.Instructions),
				(RisenStepsTabItem, RisenStepsTextBox, RisenStepsTextBox.PART_ContentTextBox, RisenStepsCountLabel, RisenType.Steps),
				(RisenEndGoalTabItem, RisenEndGoalTextBox, RisenEndGoalTextBox.PART_ContentTextBox, RisenEndGoalCountLabel, RisenType.EndGoal),
				(RisenNarrowingTabItem,  RisenNarrowingTextBox, RisenNarrowingTextBox.PART_ContentTextBox, RisenNarrowingCountLabel, RisenType.Narrowing),
			};
		}

		public (TabItem Tab, PlaceholderTextBox Holder, TextBox Box, Label Label, RisenType RisenType)[] SelectionControls;

		TextBoxData GetSelectionByBox(TextBox box)
		{
			var group = SelectionControls.FirstOrDefault(x => x.Box == box);
			if (group.Holder is null)
				return null;
			var item = DataContext as TemplateItem;
			if (item is null)
				return null;
			if (item.UiSelections is null)
				item.UiSelections = new List<TextBoxData>();
			var name = group.Holder.Name;
			var selection = item.UiSelections?.FirstOrDefault(x => x.Name == name);
			if (selection == null)
			{
				selection = new TextBoxData();
				selection.Name = name;
				item.UiSelections.Add(selection);
			}
			return selection;
		}

		public void LoadSelection(TextBox box)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var selection = GetSelectionByBox(box);
			// Set logical focus
			box.Focus();
			// Set keyboard focus
			Keyboard.Focus(box);
			if (selection is null || selection.TextLength != (box.Text?.Length ?? 0))
			{
				box.SelectionStart = box.Text?.Length ?? 0;
				return;
			}
			//box.CaretIndex = box.SelectionStart + selection.SelectionLength;
			box.SelectionStart = selection.SelectionStart;
			box.SelectionLength = selection.SelectionLength;
		}

		public void SaveSelection(TextBox box)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var selection = GetSelectionByBox(box);
			if (selection is null)
				return;
			// Cleanup selections.
			if (string.IsNullOrEmpty(box.Text))
			{
				var item = DataContext as TemplateItem;
				item.UiSelections.Remove(selection);
			}
			selection.SelectionStart = box.SelectionStart;
			selection.SelectionLength = box.SelectionLength;
			selection.TextLength = box.Text?.Length ?? 0;
		}

		#endregion

		#region Update Token Count / Usage

		public void InitTokenCounters()
		{
			DataInstructionsTextBox.PART_ContentTextBox.TextChanged += ChatPanel_DataInstructionsTextBox_TextChanged;
			DataTextBox.PART_ContentTextBox.TextChanged += ChatPanel_DataTextBox_TextChanged;
			MessagePlaceholderTextBox.PART_ContentTextBox.TextChanged += ChatPanel_MessagePlaceholderTextBox_TextChanged;
			RisenRoleTextBox.PART_ContentTextBox.TextChanged += ChatPanel_RisenRoleTextBox_TextChanged;
			RisenInstructionsTextBox.PART_ContentTextBox.TextChanged += ChatPanel_RisenInstructionsTextBox_TextChanged;
			RisenStepsTextBox.PART_ContentTextBox.TextChanged += ChatPanel_RisenStepsTextBox_TextChanged;
			RisenEndGoalTextBox.PART_ContentTextBox.TextChanged += ChatPanel_RisenEndGoalTextBox_TextChanged;
			RisenNarrowingTextBox.PART_ContentTextBox.TextChanged += ChatPanel_RisenNarrowingTextBox_TextChanged;
		}

		void UpdateTokenCount(TextBox textBox)
		{
			var label = SelectionControls.FirstOrDefault(x => x.Box == textBox).Label;
			var text = textBox.Text;
			var tokens = Companions.ClientHelper.CountTokens(text, null);
			var s = tokens == 0 ? "" : $"({tokens})";
			ControlsHelper.SetText(label, s);
		}

		private async void ChatPanel_DataInstructionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateInstructionsTokenCount, (TextBox)sender);
		private async void ChatPanel_DataTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateMessageTokenCount, (TextBox)sender);
		private async void ChatPanel_MessagePlaceholderTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateMessagePlaceholderTokenCount, (TextBox)sender);
		private async void ChatPanel_RisenRoleTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateRisenRoleTokenCount, (TextBox)sender);
		private async void ChatPanel_RisenInstructionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateRisenInstructionsTokenCount, (TextBox)sender);
		private async void ChatPanel_RisenStepsTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateRisenStepsTokenCount, (TextBox)sender);
		private async void ChatPanel_RisenEndGoalTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateRisenEndGoalTokenCount, (TextBox)sender);
		private async void ChatPanel_RisenNarrowingTextBox_TextChanged(object sender, TextChangedEventArgs e)
			=> await Helper.Debounce(UpdateRisenNarrowingTokenCount, (TextBox)sender);

		void UpdateInstructionsTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateMessageTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateMessagePlaceholderTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateRisenRoleTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateRisenInstructionsTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateRisenStepsTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateRisenEndGoalTokenCount(TextBox box)
			=> UpdateTokenCount(box);
		void UpdateRisenNarrowingTokenCount(TextBox box)
			=> UpdateTokenCount(box);

		#endregion

		#region Maximize/Restore TextBox

		private void ExpandMessageButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ControlsHelper.IsOnCooldown(sender);
			MaximizeAndNormal();
		}

		private void DataInstructionsTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (e.OriginalSource is ScrollViewer sv)
			{
				//var isVisible = sv.ComputedVerticalScrollBarVisibility == Visibility.Visible;
				//var isMax = _prevElement == InstructionsGrid;
				//ExpandInstructionsButton.Visibility = isVisible || isMax ? Visibility.Visible : Visibility.Collapsed;
				//ExpandInstructionsButton.Margin = new Thickness(3, 3, 3 + SystemParameters.VerticalScrollBarWidth, 3);
			}
		}

		private void DataTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (e.OriginalSource is ScrollViewer sv)
			{
				//var isVisible = sv.ComputedVerticalScrollBarVisibility == Visibility.Visible;
				//var isMax = _prevElement == MessageInputGrid;
				//ExpandMessageButton.Visibility = isVisible || isMax ? Visibility.Visible : Visibility.Collapsed;
				//ExpandMessageButton.Margin = new Thickness(3, 3, 3 + SystemParameters.VerticalScrollBarWidth, 3);
			}
		}

		public bool IsChatInputExpanded
		{
			get => _ChatInputExpanded;
			set { _ChatInputExpanded = value; OnPropertyChanged(); }
		}
		bool _ChatInputExpanded;


		/// <summary>
		/// Toggles between maximized and normal view for the chat input area.
		/// </summary>
		public void MaximizeAndNormal()
		{
			if (IsChatInputExpanded)
			{
				// Limit chat input.
				MessageMaxHeightOverride = null;
				UpdateMaxSize();
				ExpandRow(0);
				ExpandButtonContentControl.Content = Resources["Icon_Maximize"];
				MessagesPanelBorder.Visibility = Visibility.Visible;
			}
			else
			{
				// Expand chat input.
				MessageMaxHeightOverride = double.PositiveInfinity;
				UpdateMaxSize();
				ExpandRow(1);
				ExpandButtonContentControl.Content = Resources["Icon_Minimize"];
				MessagesPanelBorder.Visibility = Visibility.Collapsed;
			}
			IsChatInputExpanded = !IsChatInputExpanded;
		}

		/// <summary>
		/// Expands a specific row in the main grid while collapsing others.
		/// </summary>
		/// <param name="rowIndex">The index of the row to expand.</param>
		public void ExpandRow(int rowIndex)
		{
			// Iterate over all RowDefinitions in the MainGrid
			for (int i = 0; i < MainGrid.RowDefinitions.Count; i++)
			{
				if (i == rowIndex)
				{
					// Set the Height of the specified row to Star (*)
					MainGrid.RowDefinitions[i].Height = new GridLength(1, GridUnitType.Star);
				}
				else
				{
					// Set the Height of all other rows to Auto
					MainGrid.RowDefinitions[i].Height = GridLength.Auto;
				}
			}
		}

		#endregion

		TemplateItem _Item;
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				// Update from previous settings.
				if (_Item != null)
				{
					_Item.PropertyChanged -= _item_PropertyChanged;
					EnableRisen(false);
				}
				_Item = value;
				if (_Item != null)
				{
					EnableRisen(_Item.ShowRisen);
					_Item.PropertyChanged += _item_PropertyChanged;
				}
			}
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TemplateItem.ShowRisen):
					EnableRisen(_Item.ShowRisen);
					break;
				default:
					break;
			}
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		private void InstructionsPathBrowseButton_Click(object sender, RoutedEventArgs e)
		{
			var path = AssemblyInfo.ExpandPath(Item.InstructionsPath);
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				DialogHelper.AddFilter(_OpenFileDialog, ".txt");
				DialogHelper.AddFilter(_OpenFileDialog, ".md");
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			dialog.FileName = Path.GetFileName(path);
			dialog.InitialDirectory = Path.GetDirectoryName(path);
			dialog.Title = "Open Instructions File";
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			if (Item.InstructionsPathEnabled == false && string.IsNullOrWhiteSpace(Item.InstructionsPath))
				Item.InstructionsPathEnabled = true;
			Item.InstructionsPath = AssemblyInfo.ParameterizePath(dialog.FileName, true);
		}


		private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			Item = DataContext as TemplateItem;
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}

}

