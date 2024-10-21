using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Processes;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// A user control that provides functionality to select a target control or window
	/// by clicking and dragging the mouse cursor.
	/// </summary>
	public partial class TargetButtonControl : UserControl
	{
		public TargetButtonControl()
		{
			InitializeComponent();
			overlayWindow = new TargetOverlayWindow();
			overlayWindow.Title = "Overlay Window";
			overlayWindow.TargetBorder.Visibility = Visibility.Collapsed;
			highlightWindow = new TargetOverlayWindow();
			highlightWindow.Title = "Highlight Window";
			highlightWindow.TargetButton.Visibility = Visibility.Collapsed;
			mouseHandler = new MouseGlobalHook();
			mouseHandler.MouseUp += MouseHandler_MouseUp;
			mouseHandler.MouseMove += MouseHandler_MouseMove;
		}

		public event EventHandler<TargetSelectedEventArgs> TargetSelected;
		MouseGlobalHook mouseHandler;
		TargetOverlayWindow overlayWindow;
		TargetOverlayWindow highlightWindow;

		private void TargetButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (MouseGlobalHook.IsPrimaryMouseButton(MouseButton.Left))
				StartTargeting();
		}

		private void TargetButton_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (MouseGlobalHook.IsPrimaryMouseButton(MouseButton.Right))
				StartTargeting();
		}

		private void MouseHandler_MouseUp(object sender, MouseGlobalEventArgs e)
		{
			StopTargeting();
		}

		public void HighlightElement(AutomationElement element)
		{
			if (element == null)
			{
				highlightWindow.Hide();
			}
			else
			{
				// Get the bounding rectangle of the AutomationElement.
				// 'rect' is in screen coordinates (physical pixels), relative to the virtual screen.
				var rect = (Rect)element.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
				// Adjust size and location for DPI scaling.
				// Note: This may not account for per-monitor DPI scaling differences.
				var location = PositionSettings.ConvertToDiu(rect.Location);
				var size = PositionSettings.ConvertToDiu(rect.Size, rect.Location);
				highlightWindow.Width = size.Width;
				highlightWindow.Height = size.Height;
				highlightWindow.Top = location.Y;
				highlightWindow.Left = location.X;
				highlightWindow.Show();
			}
		}

		void StartTargeting()
		{
			lock (this)
			{
				System.Diagnostics.Debug.WriteLine(nameof(StartTargeting));
				TargetIcon.Visibility = Visibility.Hidden;
				var wpfPosition = PositionSettings.GetWpfCursorPosition();
				MoveOverlayWindow(wpfPosition);
				overlayWindow.Width = TargetIcon.ActualWidth;
				overlayWindow.Height = TargetIcon.ActualHeight;
				// Hide parent window
				var isCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
				if (!isCtrlDown)
				{
					overlayWindow.Show();
					var parentWindow = Window.GetWindow(this);
					parentWindow?.Hide();
				}
				mouseHandler.Start();
			}
		}

		void StopTargeting()
		{
			lock (this)
			{
				System.Diagnostics.Debug.WriteLine(nameof(StopTargeting));
				overlayWindow.Hide();
				TargetIcon.Visibility = Visibility.Visible;
				mouseHandler.Stop();
				var windowElement = _previousWindowElement;
				var editroElement = _previousEditorElement;
				HighlightElement(null);
				// Hide parent window
				var parentWindow = Window.GetWindow(this);
				parentWindow?.Show();
				// Raise the TargetSelected event
				TargetSelected?.Invoke(this, new TargetSelectedEventArgs(windowElement, editroElement));
			}
		}

		/// <summary>
		/// Move overlay window under the point.
		/// </summary>
		/// <param name="p">Position is in device units (physical pixels), relative to the virtual screen (all monitors).</param>
		void MoveOverlayWindow(Point wpfPosition)
		{
			overlayWindow.Left = wpfPosition.X - overlayWindow.Width / 2;
			overlayWindow.Top = wpfPosition.Y - overlayWindow.Height / 2;
		}

		// Add this field to keep track of the previous element under the cursor
		private AutomationElement _previousWindowElement = null;
		private AutomationElement _previousEditorElement = null;
		private Point _previousMousePosition;

		void UpdateTarget(Point position)
		{
			System.Diagnostics.Debug.WriteLine($"{nameof(UpdateTarget)}: {position}");
			// Use Dispatcher to invoke the UI Automation call on the UI thread
			Dispatcher.BeginInvoke(new Action(() =>
			{
				var wpfPosition = PositionSettings.GetWpfCursorPosition();
				MoveOverlayWindow(wpfPosition);
				// Identify the control under the cursor
				// 'point' is in device units (physical pixels), relative to the virtual screen (all monitors).
				var currentWindowElement = MouseGlobalHook.GetWindowElementFromPoint(position);
				// Get the AutomationElement directly from the screen point.
				// Note: 'AutomationElement.FromPoint' expects screen coordinates in physical pixels.
				var currentEditorElement = AutomationElement.FromPoint(position);
				_previousWindowElement = currentWindowElement;
				_previousMousePosition = position;
				WindowName.Text = ShowElementData(currentWindowElement, "Window");
				_previousEditorElement = currentEditorElement;
				_previousMousePosition = position;
				HighlightElement(currentEditorElement);
				EditorName.Text = ShowElementData(currentEditorElement, "Editor");
			}));
		}

		DateTime LastMoveOverlayWindowUpdateDate = new DateTime();

		private void MouseHandler_MouseMove(object sender, MouseGlobalEventArgs e)
		{
			var now = DateTime.UtcNow;
			if (now.Subtract(LastMoveOverlayWindowUpdateDate).TotalMilliseconds > 100)
			{
				LastMoveOverlayWindowUpdateDate = now;
				Dispatcher.BeginInvoke(new Action(() =>
				{
					var wpfPosition = PositionSettings.GetWpfCursorPosition();
					MoveOverlayWindow(wpfPosition);
				}));
			}
			_ = Helper.Debounce(UpdateTarget, e.Point, 250);
		}

		private void Overlay_TargetSelected(object sender, TargetSelectedEventArgs e)
		{
			// Show parent window again
			var parentWindow = Window.GetWindow(this);
			parentWindow?.Show();
			parentWindow?.Activate();
			// Raise the TargetSelected event
			TargetSelected?.Invoke(this, e);
		}

		public static string ShowElementData(AutomationElement element, string note = "Element")
		{
			if (element == null)
			{
				System.Diagnostics.Debug.WriteLine("No element selected");
				return "";
			}
			var name = element.Current.Name;
			var className = element.Current.ClassName;
			var controlType = element.Current.ControlType.ProgrammaticName;
			// Get the window element from the current element
			//var windowElement = GetParentWindow(element);
			var s = $"{note}: Type: {controlType}, Class: {className}, Name: {name}";
			System.Diagnostics.Debug.WriteLine(s);
			return s;
		}

		/// <summary>
		/// Traverses up the Automation tree to find the parent window of the given element.
		/// </summary>
		private static AutomationElement GetParentWindow(AutomationElement element)
		{
			AutomationElement parent = element;
			while (parent != null)
			{
				if (parent.Current.ControlType == ControlType.Window)
					return parent;
				parent = TreeWalker.RawViewWalker.GetParent(parent);
			}
			return null;
		}

	}
}
