using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JocysCom.ClassLibrary.Processes
{

	internal partial class NativeMethods
	{

		//The ToAscii function translates the specified virtual-key code and keyboard state to the corresponding character or characters. The function translates the code using the input language and physical keyboard layout identified by the keyboard layout handle.
		[DllImport("user32.dll")]
		public static extern int ToAscii(
			int uVirtKey, //[in] Specifies the virtual-key code to be translated. 
			int uScanCode, // [in] Specifies the hardware scan code of the key to be translated. The high-order bit of this value is set if the key is up (not pressed). 
			byte[] lpbKeyState, // [in] Pointer to a 256-byte array that contains the current keyboard state. Each element (byte) in the array contains the state of one key. If the high-order bit of a byte is set, the key is down (pressed). The low bit, if set, indicates that the key is toggled on. In this function, only the toggle bit of the CAPS LOCK key is relevant. The toggle state of the NUM LOCK and SCROLL LOCK keys is ignored.
			byte[] lpwTransKey, // [out] Pointer to the buffer that receives the translated character or characters. 
			int fuState
		); // [in] Specifies whether a menu is active. This parameter must be 1 if a menu is active, or 0 otherwise. 

		/// <summary>Copies the status of the 256 virtual keys to the specified buffer.</summary>
		/// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
		[DllImport("user32.dll")]
		public static extern bool GetKeyboardState(byte[] pbKeyState);

		/// <summary>Generates simple tones on the speaker.</summary>
		/// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
		[DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern bool Beep(int dwFreq, int dwDuration);

	}

	public class KeyboardHook : BaseHook
	{

		/// <summary>
		/// Start Monitoring.
		/// </summary>
		/// <param name="global">False - monitor current application only. True - monitor all.</param>
		public override void Start(bool global = false)
		{
			InstallHook(HookType.WH_KEYBOARD_LL, global);
		}

		//=====================================================================
		// Other Functions
		//---------------------------------------------------------------------

		public event KeyEventHandler KeyDown;
		public event KeyPressEventHandler KeyPress;
		public event KeyEventHandler KeyUp;
		public event EventHandler<KeyboardHookEventArgs> OnKeyboardHook;

		private const int WM_KEYDOWN = 0x100;
		private const int WM_KEYUP = 0x101;
		private const int WM_SYSKEYDOWN = 0x104;
		private const int WM_SYSKEYUP = 0x105;

		protected override IntPtr Hook1Procedure(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (EnableEvents)
			{
				var kStruct = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
				var eh = new KeyboardHookEventArgs(kStruct);
				if (OnKeyboardHook != null) OnKeyboardHook(this, eh);
				// it was ok and someone listens to events
				if ((nCode >= 0) && (KeyDown != null || KeyUp != null || KeyPress != null))
				{
					var param = wParam.ToInt32();
					// Raise KeyDown.
					if (KeyDown != null && (param == WM_KEYDOWN || param == WM_SYSKEYDOWN))
					{
						var keyData = (Keys)kStruct.vkCode;
						var e = new KeyEventArgs(keyData);
						KeyDown(this, e);
					}
					// Raise KeyPress.
					if (KeyPress != null && param == WM_KEYDOWN)
					{
						byte[] keyState = new byte[256];
						var success = NativeMethods.GetKeyboardState(keyState);
						if (!success)
						{
							var ex = new System.ComponentModel.Win32Exception();
							throw new Exception(ex.Message);
						}
						byte[] inBuffer = new byte[2];
						if (NativeMethods.ToAscii(kStruct.vkCode, kStruct.scanCode, keyState, inBuffer, kStruct.flags) == 1)
						{
							var e = new KeyPressEventArgs((char)inBuffer[0]);
							KeyPress(this, e);
						}
					}
					// Raise KeyUp.
					if (KeyUp != null && (param == WM_KEYUP || param == WM_SYSKEYUP))
					{
						var keyData = (Keys)kStruct.vkCode;
						var e = new KeyEventArgs(keyData);
						KeyUp(this, e);
					}
				}
			}
			return NativeMethods.CallNextHookEx(_Hook1Handle, nCode, wParam, lParam);
		}

	}
}
