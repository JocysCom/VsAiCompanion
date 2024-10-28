using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for AI Window.
	/// </summary>
	public partial class AiWindow : Window
	{

		public AiWindowInfo Info { get; set; }

		public AiWindow()
		{
			InitializeComponent();
		}

		private void SendButton_Click(object sender, RoutedEventArgs e)
		{
			SendMessage();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			This.Close();
		}

		#region Properties

		public static readonly DependencyProperty AttachSelectionProperty =
			DependencyProperty.Register(nameof(AttachSelection), typeof(bool), typeof(AiWindow), new PropertyMetadata(false));

		public bool AttachSelection
		{
			get { return (bool)GetValue(AttachSelectionProperty); }
			set { SetValue(AttachSelectionProperty, value); }
		}

		public static readonly DependencyProperty AttachDocumentProperty =
			DependencyProperty.Register(nameof(AttachDocument), typeof(bool), typeof(AiWindow), new PropertyMetadata(false));

		public bool AttachDocument
		{
			get { return (bool)GetValue(AttachDocumentProperty); }
			set { SetValue(AttachDocumentProperty, value); }
		}


		#endregion

		#region Show Window

		private static bool MoveTextBoxUnderMousePointer = false;

		public static AiWindow ShowUnderTheMouse()
		{
			// Get the current mouse position
			var wpfPosition = PositionSettings.GetWpfCursorPosition();
			var point = ClassLibrary.Processes.MouseGlobalHook.GetCursorPosition();
			// Create the window and set its Info
			var ai = new AiWindowInfo();
			ai.LoadInfo(point);
			// Show the window.
			var win = new AiWindow();
			win.Info = ai;
			win.Left = wpfPosition.X;
			win.Top = wpfPosition.Y;
			win.ElementPathTextBox.Text = ai.ElementPath;
			win.UpdateTokenCount();
			// Show the window first
			win.Show();
			// Adjust the window position so that the caret is under the mouse cursor
			win.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (!string.IsNullOrWhiteSpace(ai.SelectedText))
					win.SelectionCheckBox.IsChecked = true;
				else if (!string.IsNullOrWhiteSpace(ai.DocumentText))
					win.DocumentCheckBox.IsChecked = true;
				// Access the inner TextBox if DataTextBox is a custom control
				var box = win.DataTextBox.PART_ContentTextBox;
				// Get the caret position in screen coordinates
				var caretScreenPoint = win.GetCaretScreenPoint(box);
				var caretScreenPosition = ClassLibrary.Controls.PositionSettings.ConvertToDiu(caretScreenPoint);
				// Calculate the offset between the caret position and the window's current position
				if (MoveTextBoxUnderMousePointer)
				{
					// Adjust the window's Left and Top properties so that the caret is under the mouse position
					var offsetX = caretScreenPosition.X - win.Left;
					var offsetY = caretScreenPosition.Y - win.Top;
					win.Left -= -offsetX;
					win.Top -= offsetY;
				}
				// Activate the window to ensure it has focus
				win.Activate();
				// Set focus to the TextBox
				box.Focus();
				Keyboard.Focus(box);
				_ = ClassLibrary.Helper.Debounce(win.HideCursor, 100);
			}), DispatcherPriority.Loaded);
			return win;
		}

		private Point GetCaretScreenPoint(TextBox textBox)
		{
			// Ensure the layout is updated
			textBox.UpdateLayout();
			// Get the index of the caret position
			var caretIndex = textBox.CaretIndex;
			// Get the rectangle that represents the position of the caret
			var caretRect = textBox.GetRectFromCharacterIndex(caretIndex, true);
			// If caretRect is empty, set default values
			if (caretRect.IsEmpty)
				caretRect = new Rect(0, 0, 0, 0);
			// Get the position of the caret relative to the TextBox control
			var caretPositionInTextBox = new Point(caretRect.X, caretRect.Y + caretRect.Height / 2);
			// Transform the caret position to screen coordinates
			var caretScreenPosition = textBox.PointToScreen(caretPositionInTextBox);
			return caretScreenPosition;
		}

		#endregion

		void UpdateTokenCount()
		{
			// Update selection tokens count.
			var selectionTokens = Companions.ClientHelper.CountTokens(Info?.SelectedText, null);
			var selectionText = selectionTokens == 0 ? "" : $"({selectionTokens})";
			ControlsHelper.SetText(SelectionCountLabel, selectionText);
			// Update document tokens count.
			var documentTokens = Companions.ClientHelper.CountTokens(Info?.DocumentText, null);
			var documentText = documentTokens == 0 ? "" : $"({documentTokens})";
			ControlsHelper.SetText(DocumentCountLabel, documentText);
		}

		public bool UseEnterToSendMessage => Global.AppSettings.UseEnterToSendMessage;

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
					SendMessage();
				// Prevent new line added to the message.
				e.Handled = true;
			}
		}

		public bool AllowToSend()
		{
			return
				!string.IsNullOrEmpty(DataTextBox.PART_ContentTextBox.Text) ||
				(AttachSelection && !string.IsNullOrEmpty(Info.SelectedText)) ||
				(AttachDocument && !string.IsNullOrEmpty(Info.DocumentText));
		}

		void SendMessage()
		{
			var ti = Global.Templates.Items.FirstOrDefault(x => x.Name == SettingsSourceManager.TemplateAiWindowTaskName);
			var copy = ti.Copy(true);
			copy.IsPinned = false;
			copy.CanvasEditorElementPath = Info.ElementPath;
			copy.Created = DateTime.Now;
			copy.Modified = copy.Created;

			var userMessage = new Chat.MessageItem();
			userMessage.Type = Chat.MessageType.Out;
			userMessage.Body = DataTextBox.PART_ContentTextBox.Text;

			// Add element path attachment.
			var pathAttachment = new Chat.MessageAttachments();
			pathAttachment.SetData(Info.ElementPath, "xpath");
			pathAttachment.SendType = AttachmentSendType.None;
			pathAttachment.Title = "Active UI Element Path";
			userMessage.Attachments.Add(pathAttachment);

			var messageParts = new List<string>();
			if (SelectionCheckBox.IsChecked == true)
			{
				var attachment = new Chat.MessageAttachments();
				attachment.Title = "Selection";
				attachment.SetData(Info.SelectedText, "text");
				attachment.SendType = AttachmentSendType.None;
				userMessage.Attachments.Add(attachment);
			}
			if (DocumentCheckBox.IsChecked == true)
			{
				var attachment = new Chat.MessageAttachments();
				attachment.Title = "Document";
				attachment.SetData(Info.DocumentText, "text");
				attachment.SendType = AttachmentSendType.None;
				userMessage.Attachments.Add(attachment);
			}
			copy.Messages.Add(userMessage);
			Global.InsertItem(copy, ItemType.Task);
			// Select new task in the tasks list on the [Tasks] tab.
			Global.SelectTask(copy.Name);
			if (!Global.IsVsExtension)
				Global.TrayManager.RestoreFromTray(true, false);
			Close();
		}

		private void This_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Allow to close window with [Esc] key if text box is empty.
			if (e.Key == Key.Escape)
			{
				if (string.IsNullOrEmpty(DataTextBox.PART_ContentTextBox.Text))
					Close();
				e.Handled = true;
			}
		}

		#region Cursor

		void HideCursor()
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				// Hide the cursor globally within the application
				Mouse.OverrideCursor = Cursors.None;
				// Set up the mouse move event handler
				MouseMove += AiWindow_MouseMove;
				Closing += AiWindow_Closing;
				Unloaded += AiWindow_Unloaded;
				DataTextBox.PART_ContentTextBox.LostFocus += PART_ContentTextBox_LostFocus;
				Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
			}));
		}

		private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
			=> ShowCursor();

		private void PART_ContentTextBox_LostFocus(object sender, RoutedEventArgs e)
			=> ShowCursor();

		private void AiWindow_Unloaded(object sender, RoutedEventArgs e)
			=> ShowCursor();

		private void AiWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
			=> ShowCursor();

		private void AiWindow_MouseMove(object sender, MouseEventArgs e)
			=> ShowCursor();

		private void ShowCursor()
		{
			// Restore the cursor globally within the application
			Mouse.OverrideCursor = null;
			MouseMove -= AiWindow_MouseMove;
			Closing -= AiWindow_Closing;
			Unloaded -= AiWindow_Unloaded;
			DataTextBox.PART_ContentTextBox.LostFocus -= PART_ContentTextBox_LostFocus;
			Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
		}

		#endregion

	}
}
