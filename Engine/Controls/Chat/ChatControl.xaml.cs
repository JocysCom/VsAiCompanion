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

		public void FocusDataTextBox()
		{
			ControlsHelper.AppBeginInvoke(() =>
			{
				DataTextBox.PART_ContentTextBox.Focus();
				DataTextBox.PART_ContentTextBox.SelectionStart = DataTextBox.PART_ContentTextBox.Text?.Length ?? 0;
			});
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

		private void UpdateMaxSize()
		{
			if (SuspendUpdateMaxSize)
				return;
			var maxHeight = Math.Round(ActualHeight * 0.4);
			foreach (var item in SelectionControls)
				item.Box.MaxHeight = maxHeight;
		}

		private void StopButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
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
				=> await Helper.Delay(UpdateMessageFromRisen);

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

		private void Box_SelectionChanged(object sender, RoutedEventArgs e)
		{
			var box = (TextBox)sender;
			if (!box.IsFocused)
				return;
			var group = SelectionControls.FirstOrDefault(x => x.Box == box);
			// Save selection only if tab is visible.
			if (((TabControl)group.Tab.Parent).SelectedItem == group.Tab)
				SaveSelection(box);
		}

		/// <summary>
		/// Get textboxes which can store selection data.
		/// Other data must be removed.
		/// </summary>
		void InitControlsAllowedToRememer()
		{
			SelectionControls = new (TabItem, PlaceholderTextBox, TextBox)[] {
				(ChatInstructionsTabItem, DataInstructionsTextBox, DataInstructionsTextBox.PART_ContentTextBox),
				(ChatMessageTabItem, DataTextBox, DataTextBox.PART_ContentTextBox),
				(MessagePlaceholderTabItem, MessagePlaceholderTextBox, MessagePlaceholderTextBox.PART_ContentTextBox),
				(RisenRoleTabItem, RisenRoleTextBox, RisenRoleTextBox.PART_ContentTextBox),
				(RisenInstructionsTabItem, RisenInstructionsTextBox, RisenInstructionsTextBox.PART_ContentTextBox),
				(RisenStepsTabItem, RisenStepsTextBox, RisenStepsTextBox.PART_ContentTextBox),
				(RisenEndGoalTabItem, RisenEndGoalTextBox, RisenEndGoalTextBox.PART_ContentTextBox),
				(RisenNarrowingTabItem,  RisenNarrowingTextBox, RisenNarrowingTextBox.PART_ContentTextBox),
			};
		}

		(TabItem Tab, PlaceholderTextBox Holder, TextBox Box)[] SelectionControls;

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

		#region Maximize/Restore TextBox

		private void ExpandInstructionsButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			//ExpandButton(InstructionsGrid, DataInstructionsTextBox, ExpandInstructionsButton, ExpandInstructionsButtonContent);
		}

		private void ExpandMessageButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			//ExpandButton(MessageInputGrid, DataTextBox, ExpandMessageButton, ExpandMessageButtonContent);
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

		public void ExpandButton(FrameworkElement element, TextBox textBox, Button button, ContentControl buttonContent)
		{
			if (_prevElement == null)
			{
				SuspendUpdateMaxSize = true;
				textBox.MaxHeight = double.MaxValue;
				MainGrid.Visibility = Visibility.Collapsed;
				Maximize(element, ControlGrid);
				buttonContent.Content = Resources["Icon_Minimize"];
			}
			else
			{
				SuspendUpdateMaxSize = false;
				MainGrid.Visibility = Visibility.Visible;
				Restore(_prevElement);
				buttonContent.Content = Resources["Icon_Maximize"];
				UpdateMaxSize();
			}
		}

		private void Maximize(FrameworkElement element, Panel parent)
		{
			_prevParent = VisualTreeHelper.GetParent(element) as FrameworkElement;
			if (_prevParent is Panel panel)
				_prevIndex = panel.Children.IndexOf(element);
			Panel parentPanel = element.Parent as Panel;
			parentPanel.Children.Remove(element);
			parent.Children.Add(element);
			_prevElement = element;
		}

		private void Restore(FrameworkElement element)
		{
			var currentParent = element.Parent as Panel;
			currentParent.Children.Remove(element);
			if (_prevParent is Panel panel)
				panel.Children.Insert(_prevIndex, element);
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

