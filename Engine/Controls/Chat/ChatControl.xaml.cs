using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
	public partial class ChatControl : UserControl
	{
		public ChatControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			UpdateControlButtons();
			UpdateMessageEdit();
			AppControlsHelper.AllowDrop(DataTextBox.PART_ContentTextBox, true);
			AppControlsHelper.AllowDrop(DataInstructionsTextBox.PART_ContentTextBox, true);
		}

		public string EditMessageId
		{
			get { return _EditMessageId; }
			set { _EditMessageId = value; UpdateMessageEdit(); }
		}
		string _EditMessageId;

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

		public bool AllowToSend()
		{
			return
				!string.IsNullOrEmpty(DataTextBox.PART_ContentTextBox.Text) ||
				!string.IsNullOrEmpty(DataInstructionsTextBox.PART_ContentTextBox.Text);
		}

		public bool UseEnterToSendMessage { get; set; } = true;

		private void DataTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (UseEnterToSendMessage && e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
			{
				// Prevent new line added to the message.
				e.Handled = true;
			}
		}

		private void DataTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
		{
			if (UseEnterToSendMessage && e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
			{
				if (AllowToSend())
					OnSend?.Invoke(sender, e);
				// Prevent new line added to the message.
				e.Handled = true;
			}
			UpdateMessageEdit();
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

		public bool IsBusy;

		public void UpdateMessageEdit()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			SendButton.ToolTip = isEdit ? "Update message" : "Send message. Hold CTRL to add a user message and ALT to add an assistant message.";
			StopButton.ToolTip = isEdit ? "Cancel editing" : "AnimationAndMediaStop Request";
			SendButton.IsEnabled = !IsBusy && AllowToSend();
			StopButton.IsEnabled = isEdit || IsBusy;
			var sendOp = SendButton.IsEnabled ? 1.0 : 0.2;
			if (SendButton.Opacity != sendOp)
				SendButton.Opacity = sendOp;
			var stopOp = StopButton.IsEnabled ? 1.0 : 0.2;
			if (StopButton.Opacity != stopOp)
				StopButton.Opacity = stopOp;
			System.Diagnostics.Debug.WriteLine($"UpdateButtons: IsBusy={IsBusy}, isEdit={isEdit}, stopOp={stopOp}");
			if (isEdit)
			{
				var message = MessagesPanel.Messages.FirstOrDefault(x => x.Id == EditMessageId);
				if (message != null)
				{
					AttachmentsPanel.CurrentItems = message.Attachments;
				}
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
		/// If the chat form is in message edit mode, remove relevant messages before adding a new message.
		/// </summary>
		public void ApplyMessageEdit()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			if (isEdit)
			{
				var message = MessagesPanel.Messages.FirstOrDefault(x => x.Id == EditMessageId);
				if (message != null)
				{
					var messageIndex = MessagesPanel.Messages.IndexOf(message);
					if (messageIndex > -1)
					{
						var messagesToDelete = MessagesPanel.Messages.Skip(messageIndex).ToArray();
						foreach (var messageToDelete in messagesToDelete)
							MessagesPanel.RemoveMessage(messageToDelete);
					}
				}
			}
			EditMessageId = null;
		}

		private void SendButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			OnSend?.Invoke(sender, e);
		}

		private void This_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
		{
			UpdateMaxSize();
		}

		private void DataInstructionsPanel_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
		{
			UpdateMaxSize();
		}

		double? MessageMaxHeightOverride;

		private void UpdateMaxSize()
		{
			if (SuspendUpdateMaxSize)
				return;
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
				UpdateRisenFromMessage();
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
			var tab = MainTabControl.SelectedItem as TabItem;
			if (tab == null)
				return;
			var ptb = (PlaceholderTextBox)ControlsHelper.GetAll(tab, typeof(PlaceholderTextBox)).FirstOrDefault();
			if (ptb == null)
				return;
			Dispatcher.BeginInvoke(new Action(() =>
			{
				LoadSelection(ptb.PART_ContentTextBox);
			}), DispatcherPriority.Render);
		}

		public void MonitorTextBoxSelections(bool enable)
		{
			if (SelectionControls is null)
				InitControlsAllowedToRememer();
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
			SelectionControls = new (TabItem, PlaceholderTextBox, TextBox, Label)[] {
				(ChatInstructionsTabItem, DataInstructionsTextBox, DataInstructionsTextBox.PART_ContentTextBox, InstructionsCountLabel),
				(ChatMessageTabItem, DataTextBox, DataTextBox.PART_ContentTextBox, MessageCountLabel),
				(MessagePlaceholderTabItem, MessagePlaceholderTextBox, MessagePlaceholderTextBox.PART_ContentTextBox, MessagePlaceholderCountLabel ),
				(RisenRoleTabItem, RisenRoleTextBox, RisenRoleTextBox.PART_ContentTextBox, RisenRoleCountLabel),
				(RisenInstructionsTabItem, RisenInstructionsTextBox, RisenInstructionsTextBox.PART_ContentTextBox, RisenInstructionsCountLabel),
				(RisenStepsTabItem, RisenStepsTextBox, RisenStepsTextBox.PART_ContentTextBox, RisenStepsCountLabel),
				(RisenEndGoalTabItem, RisenEndGoalTextBox, RisenEndGoalTextBox.PART_ContentTextBox, RisenEndGoalCountLabel),
				(RisenNarrowingTabItem,  RisenNarrowingTextBox, RisenNarrowingTextBox.PART_ContentTextBox, RisenNarrowingCountLabel),
			};
		}

		public (TabItem Tab, PlaceholderTextBox Holder, TextBox Box, Label Label)[] SelectionControls;

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
			//if (MessageMaxHeightOverride is null)
			//{
			//	MainGrid.Visibility = Visibility.Collapsed;
			//	MessageMaxHeightOverride = 1.0;
			//	ExpandButtonContentControl.Content = Resources["Icon_Minimize"];
			//}
			//else
			//{
			//	MessageMaxHeightOverride = null;
			//	ExpandButtonContentControl.Content = Resources["Icon_Maximize"];
			//	MainGrid.Visibility = Visibility.Visible;
			//}
			//UpdateMaxSize();
			ApplyExpand(ChatInputGrid, ExpandButton, ExpandButtonContentControl);
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

		public bool SuspendUpdateMaxSize;
		public int MaximizedControl;

		FrameworkElement _prevParent;
		FrameworkElement _prevElement;
		int _prevIndex;

		public void ApplyExpand(FrameworkElement element, Button button, ContentControl buttonContent)
		{
			if (_prevElement == null)
			{
				MessageMaxHeightOverride = 1.0;
				UpdateMaxSize();
				SuspendUpdateMaxSize = true;
				MainGrid.Visibility = Visibility.Collapsed;
				Maximize(element, ControlGrid);
				buttonContent.Content = Resources["Icon_Minimize"];
			}
			else
			{
				SuspendUpdateMaxSize = false;
				MessageMaxHeightOverride = null;
				UpdateMaxSize();
				MainGrid.Visibility = Visibility.Visible;
				Restore(_prevElement);
				buttonContent.Content = Resources["Icon_Maximize"];
			}
		}

		private void Maximize(FrameworkElement element, Panel newParent)
		{
			_prevParent = VisualTreeHelper.GetParent(element) as FrameworkElement;
			if (_prevParent is Panel panel)
				_prevIndex = panel.Children.IndexOf(element);
			// Remove from current parent.
			ControlsHelper.RemoveFromParent(element);
			newParent.Children.Add(element);
			_prevElement = element;
		}

		private void Restore(FrameworkElement element)
		{
			// Remove from current parent.
			ControlsHelper.RemoveFromParent(element);
			if (_prevParent is Panel panel)
				panel.Children.Insert(_prevIndex, element);
			if (_prevParent is Border border)
				border.Child = element;
			_prevElement = null;
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


		private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			Item = DataContext as TemplateItem;
		}
	}

}

