using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Linq;

namespace JocysCom.ClassLibrary.Controls.Chat
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
			UpdateButtons();
			MessagesPanel.ScriptingHandler.OnMessageAction += ScriptingHandler_OnMessageAction;
			//MessagesPanel.Messages
			//InfoPanel.Tasks.ListChanged += Tasks_ListChanged;
		}

		private void ScriptingHandler_OnMessageAction(object sender, string[] e)
		{
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), e[1]);
			if (action != MessageAction.Use && action != MessageAction.Edit)
				return;
			var id = e[0];
			var message = MessagesPanel.Messages.FirstOrDefault(x => x.Id == id);
			if (message == null)
				return;
			if (action == MessageAction.Use)
			{
				DataTextBox.Text = message.Body;
				EditMessageId = null;
				FocusDataTextBox();
			}
			else if (action == MessageAction.Edit)
			{
				DataTextBox.Text = message.Body;
				EditMessageId = id;
				FocusDataTextBox();
			}
		}

		public void FocusDataTextBox()
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				DataTextBox.Focus();
				DataTextBox.SelectionStart = DataTextBox.Text?.Length ?? 0;
			}));
		}

		public string EditMessageId
		{
			get { return _EditMessageId; }
			set { _EditMessageId = value; UpdateButtons(); }
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

		private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
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
				!string.IsNullOrEmpty(DataTextBox.Text) ||
				!string.IsNullOrEmpty(DataInstructionsTextBox.Text);
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
			UpdateButtons();
		}

		private void DataInstructionsTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
		{
			UpdateButtons();
		}

		private void DataInstructionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateButtons();
		}

		private void DataTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateButtons();
		}

		public bool IsBusy;

		public void UpdateButtons()
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			SendButton.ToolTip = isEdit ? "Update Message" : "Send Message";
			StopButton.ToolTip = isEdit ? "Cancel Editing" : "Stop Request";
			SendButton.IsEnabled = !IsBusy && AllowToSend();
			StopButton.IsEnabled = isEdit || IsBusy;
			var sendOp = !IsBusy || AllowToSend() ? 1.0 : 0.2;
			if (SendButton.Opacity != sendOp)
				SendButton.Opacity = sendOp;
			var stopOp = IsBusy || isEdit ? 1.0 : 0.2;
			if (StopButton.Opacity != stopOp)
				StopButton.Opacity = stopOp;
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
			var maxHeight = ActualHeight;
			DataInstructionsTextBox.MaxHeight = Math.Round(maxHeight * 0.3);
			DataTextBox.MaxHeight = Math.Round(maxHeight * 0.4);
		}

		private void StopButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var isEdit = !string.IsNullOrEmpty(EditMessageId);
			if (isEdit)
			{
				DataTextBox.Text = "";
				EditMessageId = null;
				UpdateButtons();
			}
			else
			{
				OnStop?.Invoke(sender, e);
			}
		}

	}

}

