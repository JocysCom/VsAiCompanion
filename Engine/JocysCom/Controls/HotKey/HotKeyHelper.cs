using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace JocysCom.ClassLibrary.Controls.HotKey
{

	public class HotKeyHelper : IDisposable
	{
		private const int WM_HOTKEY = 0x0312;
		private readonly IntPtr _windowHandle;
		private int _hotKeyId;
		private bool _isKeyRegistered = false;

		public event EventHandler HotKeyPressed;

		public HotKeyHelper(Window window)
		{
			// Get the window handle
			var helper = new WindowInteropHelper(window);
			_windowHandle = helper.Handle;

			// Add hook for window messages
			ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
		}

		public bool RegisterHotKey(ModifierKeys modifierKeys, System.Windows.Input.Key key)
		{
			if (_isKeyRegistered)
				UnregisterHotKey();

			_hotKeyId = GetType().GetHashCode();
			uint virtualKeyCode = (uint)KeyInterop.VirtualKeyFromKey(key);

			bool isKeyRegistered = NativeMethods.RegisterHotKey(_windowHandle, _hotKeyId, (uint)modifierKeys, virtualKeyCode);
			_isKeyRegistered = isKeyRegistered;

			return isKeyRegistered;
		}

		public void UnregisterHotKey()
		{
			if (_isKeyRegistered)
			{
				NativeMethods.UnregisterHotKey(_windowHandle, _hotKeyId);
				_isKeyRegistered = false;
			}
		}

		private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
		{
			if (msg.message == WM_HOTKEY && (int)msg.wParam == _hotKeyId)
			{
				HotKeyPressed?.Invoke(this, EventArgs.Empty);
				handled = true;
			}
		}

		public void Dispose()
		{
			UnregisterHotKey();
			ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcher_ThreadPreprocessMessage;
		}

		private static class NativeMethods
		{
			// DLL imports for hotkey registration/unregistration
			[DllImport("user32.dll")]
			public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

			[DllImport("user32.dll")]
			public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
		}
	}


}
