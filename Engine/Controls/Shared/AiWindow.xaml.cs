using JocysCom.ClassLibrary.Controls;
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

		public static AiWindow ShowUnderTheMouse()
		{
			// Get the current mouse position
			var point = ClassLibrary.Processes.MouseGlobalHook.GetCursorPosition();
			var position = ClassLibrary.Controls.PositionSettings.ConvertToDiu(point);
			// Create the window and set its Info
			var ai = new AiWindowInfo();
			ai.LoadInfo(point);
			// Show the window.
			var win = new AiWindow();
			win.Info = ai;
			win.Left = position.X;
			win.Top = position.Y;
			win.ElementPathTextBox.Text = ai.ElementPath;
			win.UpdateTokenCount();
			// Show the window first
			win.Show();
			// Activate the window to ensure it has focus
			win.Activate();
			// Adjust the window position so that the caret is under the mouse cursor
			win.Dispatcher.BeginInvoke(new Action(() =>
			{
				// Access the inner TextBox if DataTextBox is a custom control
				var box = win.DataTextBox.PART_ContentTextBox;
				// Set focus to the TextBox
				box.Focus();
				Keyboard.Focus(box);
				box.Text = " ";
				box.Text = "";
				// Get the caret position in screen coordinates
				var caretScreenPoint = win.GetCaretScreenPoint(box);
				var caretScreenPosition = ClassLibrary.Controls.PositionSettings.ConvertToDiu(caretScreenPoint);
				// Calculate the offset between the caret position and the window's current position
				var offsetX = caretScreenPosition.X - win.Left;
				var offsetY = caretScreenPosition.Y - win.Top;
				// Adjust the window's Left and Top properties so that the caret is under the mouse position
				win.Left = position.X - offsetX;
				win.Top = position.Y - offsetY;
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
			ControlsHelper.SetText(SelectionCountLabel, documentText);
		}

		#region

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
			var selection = new List<string>();
			var copy = ti.Copy(true);
			copy.IsPinned = false;
			copy.CanvasEditorElementPath = Info.ElementPath;
			copy.Created = DateTime.Now;
			copy.Modified = copy.Created;
			Global.InsertItem(copy, ItemType.Task);
			selection.Add(copy.Name);
			// Select new task in the tasks list on the [Tasks] tab.
			var settings = Global.AppSettings.GetTaskSettings(ItemType.Task);
			settings.ListSelection = selection;
			settings.Focus = true;
			Global.RaiseOnTasksUpdated();
		}

		#endregion

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

	}
}
