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
			var id = e[0];
			var message = MessagesPanel.Messages.FirstOrDefault(x => x.Id == id);
			if (message == null)
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), e[1]);
			if (action == MessageAction.Use)
				DataTextBox.Text = message.Body;
		}

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

		private void DataTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
			{
				if (AllowToSend())
					OnSend?.Invoke(sender, e);
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
			var sendOp = IsBusy ? 0.2 : AllowToSend() ? 1.0 : 0.5;
			if (SendButton.Opacity != sendOp)
				SendButton.Opacity = sendOp;
			var stopOp = IsBusy ? 1.0 : 0.2;
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
			OnStop?.Invoke(sender, e);
        }

	}

}

