using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace JocysCom.ClassLibrary.Processes
{
	public class MouseGlobalHandler
	{
		// Delegate for the hook callback
		private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

		// Import SetWindowsHookEx from user32.dll
		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
			IntPtr hMod, uint dwThreadId);

		// Import UnhookWindowsHookEx from user32.dll
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool UnhookWindowsHookEx(IntPtr hHook);

		// Import CallNextHookEx from user32.dll
		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode,
			IntPtr wParam, IntPtr lParam);

		// Import GetModuleHandle from kernel32.dll
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);

		// Constants
		private const int WH_MOUSE_LL = 14;
		private const int WM_MOUSEMOVE = 0x0200;
		private const int WM_LBUTTONDOWN = 0x0201;
		private const int WM_LBUTTONUP = 0x0202;
		private const int WM_RBUTTONDOWN = 0x0204;
		private const int WM_RBUTTONUP = 0x0205;

		private static IntPtr _hookHandle = IntPtr.Zero;
		private static LowLevelMouseProc _procDelegate;

		// Events for mouse actions
		public event EventHandler<GlobalMouseEventArgs> MouseMove;
		public event EventHandler<GlobalMouseEventArgs> MouseLeftButtonDown;
		public event EventHandler<GlobalMouseEventArgs> MouseLeftButtonUp;
		public event EventHandler<GlobalMouseEventArgs> MouseRightButtonDown;
		public event EventHandler<GlobalMouseEventArgs> MouseRightButtonUp;

		// Start listening for mouse events
		public void Start()
		{
			if (_hookHandle != IntPtr.Zero)
				return; // Already hooked

			_procDelegate = HookCallback;
			using (Process curProcess = Process.GetCurrentProcess())
			using (ProcessModule curModule = curProcess.MainModule)
			{
				IntPtr hMod = GetModuleHandle(curModule.ModuleName);
				_hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _procDelegate, hMod, 0);

				if (_hookHandle == IntPtr.Zero)
				{
					int errorCode = Marshal.GetLastWin32Error();
					throw new System.ComponentModel.Win32Exception(errorCode);
				}
			}
		}

		// Stop listening for mouse events
		public void Stop()
		{
			if (_hookHandle != IntPtr.Zero)
			{
				bool result = UnhookWindowsHookEx(_hookHandle);
				if (!result)
				{
					int errorCode = Marshal.GetLastWin32Error();
					throw new System.ComponentModel.Win32Exception(errorCode);
				}
				_hookHandle = IntPtr.Zero;
			}
		}

		// Callback method for mouse hook
		private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0)
			{
				// Marshal the mouse input data
				MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

				int x = hookStruct.pt.x;
				int y = hookStruct.pt.y;

				var eventArgs = new GlobalMouseEventArgs(x, y);

				switch ((int)wParam)
				{
					case WM_MOUSEMOVE:
						MouseMove?.Invoke(this, eventArgs);
						break;
					case WM_LBUTTONDOWN:
						MouseLeftButtonDown?.Invoke(this, eventArgs);
						break;
					case WM_LBUTTONUP:
						MouseLeftButtonUp?.Invoke(this, eventArgs);
						break;
					case WM_RBUTTONDOWN:
						MouseRightButtonDown?.Invoke(this, eventArgs);
						break;
					case WM_RBUTTONUP:
						MouseRightButtonUp?.Invoke(this, eventArgs);
						break;
				}
			}

			return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
		}

		// Struct for mouse input data
		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MSLLHOOKSTRUCT
		{
			public POINT pt; // The x and y coordinates
			public uint mouseData;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		// Class for mouse event args
		public class GlobalMouseEventArgs : EventArgs
		{
			public int X { get; }
			public int Y { get; }

			// You can add more properties as needed

			public GlobalMouseEventArgs(int x, int y)
			{
				X = x;
				Y = y;
			}

			public AutomationElement GetElementUnderMouse()
			{
				// Use UIAutomation to get the element under the mouse cursor
				var point = new System.Windows.Point(X, Y);
				var element = AutomationElement.FromPoint(point);
				return element;
			}
		}
	}
}
