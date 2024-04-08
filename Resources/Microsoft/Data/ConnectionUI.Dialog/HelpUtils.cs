using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	internal sealed class HelpUtils
	{
		private const int KeyValueNameLength = 1024;

		private HelpUtils()
		{
		}

		public static bool IsContextHelpMessage(ref Message m)
		{
			return m.Msg == 274 && ((int)m.WParam & 65520) == 61824;
		}

		public static bool IsWow64()
		{
			bool pIsWow64 = false;
			if (Environment.OSVersion.Version.Major >= 5)
			{
				Process currentProcess = Process.GetCurrentProcess();
				try
				{
					NativeMethods.IsWow64Process(currentProcess.Handle, out pIsWow64);
				}
				catch (Exception)
				{
					pIsWow64 = false;
				}
			}
			return pIsWow64;
		}

		public static string[] GetValueNamesWow64(string registryKey, int ulOptions)
		{
			IntPtr zero = IntPtr.Zero;
			UIntPtr phkResult = UIntPtr.Zero;
			int num = 0;
			string[] strArray = (string[])null;
			try
			{
				num = NativeMethods.RegOpenKeyEx(NativeMethods.HKEY_LOCAL_MACHINE, registryKey, 0, ulOptions, out phkResult);
			}
			catch
			{
			}
			if (num == 0 && !object.Equals((object)phkResult, (object)UIntPtr.Zero))
			{
				uint lpcValues = 0;
				try
				{
					num = NativeMethods.RegQueryInfoKey(phkResult, (byte[])null, IntPtr.Zero, IntPtr.Zero, out uint _, IntPtr.Zero, IntPtr.Zero, out lpcValues, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				}
				catch
				{
				}
				if (num == 0)
				{
					strArray = new string[(int)lpcValues];
					for (uint index = 0; index < lpcValues; ++index)
					{
						StringBuilder lpValueName = new StringBuilder(1024);
						uint lpcbValueName = 1024;
						try
						{
							num = NativeMethods.RegEnumValue(phkResult, index, lpValueName, ref lpcbValueName, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
						}
						catch
						{
						}
						if (num == 0)
							strArray[(int)index] = lpValueName.ToString();
					}
				}
			}
			return strArray ?? new string[0];
		}

		public static void TranslateContextHelpMessage(Form f, ref Message m)
		{
			Control activeControl = HelpUtils.GetActiveControl(f);
			if (activeControl == null)
				return;
			m.HWnd = activeControl.Handle;
			m.Msg = 83;
			m.WParam = IntPtr.Zero;
			NativeMethods.HELPINFO structure = new NativeMethods.HELPINFO();
			structure.iContextType = 1;
			structure.iCtrlId = f.Handle.ToInt32();
			structure.hItemHandle = activeControl.Handle;
			structure.dwContextId = 0;
			structure.MousePos.x = (int)NativeMethods.LOWORD((int)m.LParam);
			structure.MousePos.y = (int)NativeMethods.HIWORD((int)m.LParam);
			m.LParam = Marshal.AllocHGlobal(Marshal.SizeOf((object)structure));
			Marshal.StructureToPtr((object)structure, m.LParam, false);
		}

		public static Control GetActiveControl(Form f)
		{
			Control activeControl = (Control)f;
			while (activeControl is ContainerControl containerControl && containerControl.ActiveControl != null)
				activeControl = containerControl.ActiveControl;
			return activeControl;
		}
	}
}
