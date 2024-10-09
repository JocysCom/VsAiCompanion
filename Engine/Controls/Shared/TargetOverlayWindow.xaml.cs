using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for TargetOverlayWindow.xaml
	/// </summary>
	public partial class TargetOverlayWindow : Window
	{
		public event EventHandler<TargetSelectedEventArgs> TargetSelected;

		public TargetOverlayWindow()
		{
			InitializeComponent();
			var mouseHook = new JocysCom.ClassLibrary.Processes.MouseHook();

			// Capture mouse events
			MouseLeftButtonUp += OverlayWindow_MouseLeftButtonUp;
			MouseMove += OverlayWindow_MouseMove;
			// Ensure the window captures mouse events even if it's transparent
			IsHitTestVisible = true;
		}


		private void OverlayWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			// Get the cursor position
			System.Windows.Point position = e.GetPosition(this);
			System.Drawing.Point screenPoint = new System.Drawing.Point(
				(int)(Left + position.X),
				(int)(Top + position.Y));
			// Identify the control under the cursor
			var element = AutomationElement.FromPoint(new System.Windows.Point(screenPoint.X, screenPoint.Y));
			// Get the window element from the element under the cursor.
			var windowElement = GetParentWindow(element);
			// Close the overlay window
			Close();
			// Raise the TargetSelected event
			TargetSelected?.Invoke(this, new TargetSelectedEventArgs(windowElement, element));
		}

		public void ExpandWindow()
		{
			Left = SystemParameters.VirtualScreenLeft;
			Top = SystemParameters.VirtualScreenTop;
			Width = SystemParameters.VirtualScreenWidth;
			Height = SystemParameters.VirtualScreenHeight;
			CanvasPanel.Width = SystemParameters.VirtualScreenWidth;
			CanvasPanel.Height = SystemParameters.VirtualScreenHeight;
		}

		// Add this field to keep track of the previous element under the cursor
		private AutomationElement _previousElement = null;

		private void OverlayWindow_MouseMove(object sender, MouseEventArgs e)
		{
			// Get the cursor position relative to the overlay window
			System.Windows.Point position = e.GetPosition(this);
			// Convert to screen coordinates
			System.Drawing.Point screenPoint = new System.Drawing.Point(
				(int)(Left + position.X),
				(int)(Top + position.Y));

			// Identify the control under the cursor
			var currentElement = AutomationElement.FromPoint(
				new System.Windows.Point(screenPoint.X, screenPoint.Y));

			// Check if the element has changed
			if (!Equals(currentElement, _previousElement))
			{
				ShowElementData(currentElement);
				// Update the previous element
				_previousElement = currentElement;
			}
		}

		public static void ShowElementData(AutomationElement element)
		{
			if (element == null)
			{
				System.Diagnostics.Debug.WriteLine("No element selected");
				return;
			}
			var elementName = element.Current.Name;
			var controlType = element.Current.ControlType.ProgrammaticName;
			// Get the window element from the current element
			var windowElement = GetParentWindow(element);
			var windowTitle = windowElement?.Current.Name ?? "Unknown Window";
			System.Diagnostics.Debug.WriteLine(
				$"Selected Element: {elementName} [{controlType}] in Window: {windowTitle}");
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

