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
		public event EventHandler<TargetSelectedEventArgs> TargetSelected;

		public TargetButtonControl()
		{
			InitializeComponent();
			mouseHandler = new MouseGlobalHandler();
			mouseHandler.MouseLeftButtonUp += GlobalMouseHandler_MouseLeftButtonUp;
			mouseHandler.MouseMove += MouseHandler_MouseMove;
		}


		private void TargetButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("TargetButton_PreviewMouseLeftButtonDown");
			// Hide parent window
			var parentWindow = Window.GetWindow(this);
			parentWindow?.Hide();
			mouseHandler.Start();
		}

		MouseGlobalHandler mouseHandler;

		private void GlobalMouseHandler_MouseLeftButtonUp(object sender, EventArgs e)
		{
			mouseHandler.Stop();

			System.Diagnostics.Debug.WriteLine("MouseHook_OnMouseUp");
			// Get the cursor position
			var screenPoint = System.Windows.Forms.Cursor.Position;
			// Identify the control under the cursor
			var element = AutomationElement.FromPoint(new System.Windows.Point(screenPoint.X, screenPoint.Y));
			// Get the window element from the element under the cursor.
			var windowElement = GetParentWindow(element);
			// Hide parent window
			var parentWindow = Window.GetWindow(this);
			parentWindow?.Show();
			// Raise the TargetSelected event
			TargetSelected?.Invoke(this, new TargetSelectedEventArgs(windowElement, element));
		}


		// Add this field to keep track of the previous element under the cursor
		private AutomationElement _previousElement = null;

		private void MouseHandler_MouseMove(object sender, MouseGlobalHandler.GlobalMouseEventArgs e)
		{

			// Get the cursor position relative to the overlay window
			// Convert to screen coordinates
			var screenPoint = System.Windows.Forms.Cursor.Position;

			AutomationElement currentElement = null;
			try
			{
				// Identify the control under the cursor
				currentElement = AutomationElement.FromPoint(
					new System.Windows.Point(screenPoint.X, screenPoint.Y));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.Write(ex.Message);
			}

			// Check if the element has changed
			if (!Equals(currentElement, _previousElement))
			{
				ShowElementData(currentElement);
				// Update the previous element
				_previousElement = currentElement;
			}
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
