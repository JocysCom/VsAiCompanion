using System;
using System.Collections.Generic;
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

		public bool RegisterHotKey(string hotKey)
		{
			ModifierKeys modifiers;
			Key key;
			var success = TryParseHotKey(hotKey, out key, out modifiers);
			if (success)
				RegisterHotKey(modifiers, key);
			return success;
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

		public static string HotKeyToString(ModifierKeys modifiers, Key key)
		{
			// Capture modifier keys
			var keys = new List<string>();
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				keys.Add("CTRL");
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
				keys.Add("ALT");
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				keys.Add("SHIFT");
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
				keys.Add("WIN");
			keys.Add(key.ToString());
			var hotKey = string.Join("+", keys);
			return hotKey;
		}

		public static bool TryParseHotKey(string hotkeyString, out Key key, out ModifierKeys modifiers)
		{
			key = Key.None;
			modifiers = 0;
			if (string.IsNullOrEmpty(hotkeyString))
				return false;
			var parts = hotkeyString.ToUpper().Split('+');
			foreach (string part in parts)
			{
				switch (part)
				{
					case "CTRL":
						modifiers |= ModifierKeys.Control;
						break;
					case "ALT":
						modifiers |= ModifierKeys.Alt;
						break;
					case "SHIFT":
						modifiers |= ModifierKeys.Shift;
						break;
					case "WIN":
						modifiers |= ModifierKeys.Windows;
						break;
					default:
						if (Enum.TryParse(part, out Key parsedKey))
							key = parsedKey;
						else
							return false;
						break;
				}
			}
			return true;
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
