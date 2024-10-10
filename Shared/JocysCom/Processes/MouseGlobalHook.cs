using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace JocysCom.ClassLibrary.Processes
{
	/// <summary>
	/// Class to handle global mouse events.
	/// </summary>
	public class MouseGlobalHook : IDisposable
	{
		#region WinAPI Imports and Constants

		// Delegate for the hook callback
		private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

		private const int WH_MOUSE_LL = 14;

		// Mouse message constants
		private const int WM_MOUSEMOVE = 0x0200;
		private const int WM_LBUTTONDOWN = 0x0201;
		private const int WM_LBUTTONUP = 0x0202;
		private const int WM_LBUTTONDBLCLK = 0x0203;
		private const int WM_RBUTTONDOWN = 0x0204;
		private const int WM_RBUTTONUP = 0x0205;
		private const int WM_RBUTTONDBLCLK = 0x0206;
		private const int WM_MBUTTONDOWN = 0x0207;
		private const int WM_MBUTTONUP = 0x0208;
		private const int WM_MBUTTONDBLCLK = 0x0209;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(
			int idHook,
			LowLevelMouseProc lpfn,
			IntPtr hMod,
			uint dwThreadId);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool UnhookWindowsHookEx(IntPtr hHook);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr CallNextHookEx(
			IntPtr hHook,
			int nCode,
			IntPtr wParam,
			IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("user32.dll")]
		static extern bool GetCursorPos(out POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern IntPtr WindowFromPoint(POINT Point);

		[DllImport("user32.dll")]
		private static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint uFlags);

		#endregion

		#region Private Members

		private IntPtr _hookHandle = IntPtr.Zero;
		private LowLevelMouseProc _procDelegate;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the mouse is moved.
		/// </summary>
		public event EventHandler<MouseGlobalEventArgs> MouseMove;

		/// <summary>
		/// Occurs when a mouse button is pressed.
		/// </summary>
		public event EventHandler<MouseGlobalEventArgs> MouseDown;

		/// <summary>
		/// Occurs when a mouse button is released.
		/// </summary>
		public event EventHandler<MouseGlobalEventArgs> MouseUp;

		#endregion

		#region Public Methods

		/// <summary>
		/// Starts listening for global mouse events.
		/// </summary>
		public void Start()
		{
			// If already hooked then return.
			if (_hookHandle != IntPtr.Zero)
				return;
			_procDelegate = HookCallback;
			// Use GetModuleHandle with null to get handle to the current module
			IntPtr hMod = GetModuleHandle(null);
			_hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _procDelegate, hMod, 0);
			if (_hookHandle == IntPtr.Zero)
			{
				int errorCode = Marshal.GetLastWin32Error();
				throw new System.ComponentModel.Win32Exception(errorCode);
			}
		}

		/// <summary>
		/// Stops listening for global mouse events.
		/// </summary>
		public void Stop()
		{
			// If not hooked then return.
			if (_hookHandle == IntPtr.Zero)
				return;
			bool result = UnhookWindowsHookEx(_hookHandle);
			if (!result)
			{
				int errorCode = Marshal.GetLastWin32Error();
				throw new System.ComponentModel.Win32Exception(errorCode);
			}
			_hookHandle = IntPtr.Zero;
			_procDelegate = null;
		}

		#endregion

		#region Private Methods

		private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0)
				_HookCallback(wParam, lParam);
			return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
		}

		/// <summary>
		/// Callback method for mouse hook.
		/// </summary>
		private void _HookCallback(IntPtr wParam, IntPtr lParam)
		{
			// Marshal the mouse input data
			var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
			// Create a Point from the coordinates
			var point = new Point(hookStruct.pt.x, hookStruct.pt.y);
			var buttonState = MouseButtonState.Released;
			var changedButton = MouseButton.Left; // Default value
			var isMouseButtonEvent = false;
			var clickCount = 1;
			switch ((int)wParam)
			{
				case WM_MOUSEMOVE:
					MouseMove?.Invoke(this, new MouseGlobalEventArgs(point));
					break;
				case WM_LBUTTONDOWN:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Left;
					isMouseButtonEvent = true;
					break;
				case WM_LBUTTONUP:
					buttonState = MouseButtonState.Released;
					changedButton = MouseButton.Left;
					isMouseButtonEvent = true;
					break;
				case WM_LBUTTONDBLCLK:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Left;
					isMouseButtonEvent = true;
					clickCount = 2;
					break;
				case WM_RBUTTONDOWN:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Right;
					isMouseButtonEvent = true;
					break;
				case WM_RBUTTONUP:
					buttonState = MouseButtonState.Released;
					changedButton = MouseButton.Right;
					isMouseButtonEvent = true;
					break;
				case WM_RBUTTONDBLCLK:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Right;
					isMouseButtonEvent = true;
					clickCount = 2;
					break;
				case WM_MBUTTONDOWN:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Middle;
					isMouseButtonEvent = true;
					break;
				case WM_MBUTTONUP:
					buttonState = MouseButtonState.Released;
					changedButton = MouseButton.Middle;
					isMouseButtonEvent = true;
					break;
				case WM_MBUTTONDBLCLK:
					buttonState = MouseButtonState.Pressed;
					changedButton = MouseButton.Middle;
					isMouseButtonEvent = true;
					clickCount = 2;
					break;
					// Handle other mouse events as needed
			}
			if (isMouseButtonEvent)
			{
				var eventArgs = new MouseGlobalEventArgs(point, changedButton, buttonState, clickCount);
				if (buttonState == MouseButtonState.Pressed)
					MouseDown?.Invoke(this, eventArgs);
				else if (buttonState == MouseButtonState.Released)
					MouseUp?.Invoke(this, eventArgs);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Get cursor position.
		/// </summary>
		public static Point GetCursorPosition()
		{
			POINT point;
			// Get the cursor position
			return GetCursorPos(out point)
				? new Point(point.x, point.y)
				: new Point();
		}

		/// <summary>
		/// Return true if button is primary.
		/// </summary>
		public static bool IsPrimaryMouseButton(MouseButton button)
		{
			return SystemParameters.SwapButtons
			   ? button == MouseButton.Right
			   : button == MouseButton.Left;
		}

		public static AutomationElement GetWindowElementFromPoint(Point point)
		{
			AutomationElement currentElement = null;
			// Convert System.Windows.Point to native POINT
			var nativePoint = new POINT();
			nativePoint.x = (int)point.X;
			nativePoint.y = (int)point.Y;

			// Get the window handle under the cursor
			IntPtr hWnd = WindowFromPoint(nativePoint);

			if (hWnd != IntPtr.Zero)
			{
				// Use the window handle to get the AutomationElement
				currentElement = AutomationElement.FromHandle(hWnd);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("No window found at the point.");
			}
			return currentElement;
		}

		#endregion

		#region Structs

		// Struct for mouse input data
		[StructLayout(LayoutKind.Sequential)]
		private struct MSLLHOOKSTRUCT
		{
			public POINT pt;              // The x and y coordinates
			public uint mouseData;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		#endregion

		#region IDisposable Implementation

		private bool _disposed = false;

		/// <summary>
		/// Releases all resources used by the <see cref="MouseGlobalHook"/>.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Disposes the resources used by the <see cref="MouseGlobalHook"/> class.
		/// </summary>
		/// <param name="disposing">True if managed resources should be disposed.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				Stop();
				_disposed = true;
			}
		}

		#endregion
	}
}
