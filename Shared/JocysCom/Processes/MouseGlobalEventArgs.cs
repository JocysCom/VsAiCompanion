using System;
using System.Windows;
using System.Windows.Input;

namespace JocysCom.ClassLibrary.Processes
{
	/// <summary>
	/// Provides data for global mouse events.
	/// </summary>
	public class MouseGlobalEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the position of the mouse.
		/// </summary>
		public Point Point { get; }

		/// <summary>
		/// Gets the button associated with the event.
		/// </summary>
		public MouseButton ChangedButton { get; }

		/// <summary>
		/// Gets the state of the button associated with the event.
		/// </summary>
		public MouseButtonState ButtonState { get; }

		/// <summary>
		/// Gets the number of times the mouse button was clicked.
		/// </summary>
		public int ClickCount { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MouseGlobalEventArgs"/> class for mouse move events.
		/// </summary>
		/// <param name="point">The position of the mouse.</param>
		public MouseGlobalEventArgs(Point point)
		{
			Point = point;
			ChangedButton = MouseButton.Left;
			ButtonState = MouseButtonState.Released;
			ClickCount = 0;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MouseGlobalEventArgs"/> class for mouse button events.
		/// </summary>
		/// <param name="point">The position of the mouse.</param>
		/// <param name="changedButton">The button associated with the event.</param>
		/// <param name="buttonState">The state of the button.</param>
		/// <param name="clickCount">The number of clicks.</param>
		public MouseGlobalEventArgs(
			Point point,
			MouseButton changedButton,
			MouseButtonState buttonState,
			int clickCount = 1)
		{
			Point = point;
			ChangedButton = changedButton;
			ButtonState = buttonState;
			ClickCount = clickCount;
		}
	}
}
