﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Data.ConnectionUI
{
  internal sealed class NativeMethods
  {
    internal const int KEY_WOW64_64KEY = 256;
    internal const int KEY_WOW64_32KEY = 512;
    internal const int KEY_QUERY_VALUE = 1;
    internal const int DB_E_CANCELED = -2147217842;
    internal const int CLSCTX_INPROC_SERVER = 1;
    internal const int WM_SETFOCUS = 7;
    internal const int WM_HELP = 83;
    internal const int WM_CONTEXTMENU = 123;
    internal const int WM_SYSCOMMAND = 274;
    internal const int SC_CONTEXTHELP = 61824;
    internal const int HELPINFO_WINDOW = 1;
    internal const int DBSOURCETYPE_DATASOURCE_TDP = 1;
    internal const int DBSOURCETYPE_DATASOURCE_MDP = 3;
    internal const int DBPROMPTOPTIONS_PROPERTYSHEET = 2;
    internal const int DBPROMPTOPTIONS_DISABLE_PROVIDER_SELECTION = 16;
    internal const ushort SQL_DRIVER_PROMPT = 2;
    internal const short SQL_NO_DATA = 100;
    internal static readonly UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(2147483650U);
    internal static Guid IID_IUnknown = new Guid("00000000-0000-0000-c000-000000000046");
    internal static Guid CLSID_DataLinks = new Guid("2206CDB2-19C1-11d1-89E0-00C04FD7A829");
    internal static Guid CLSID_OLEDB_ENUMERATOR = new Guid("C8B522D0-5CF3-11ce-ADE5-00AA0044773D");
    internal static Guid CLSID_MSDASQL_ENUMERATOR = new Guid("C8B522CD-5CF3-11ce-ADE5-00AA0044773D");

    private NativeMethods()
    {
    }

    internal static bool SQL_SUCCEEDED(short rc) => ((int) rc & -2) == 0;

    internal static short LOWORD(int dwValue) => (short) (dwValue & (int) ushort.MaxValue);

    internal static short HIWORD(int dwValue) => (short) (dwValue >> 16 & (int) ushort.MaxValue);

    [DllImport("odbc32.dll")]
    internal static extern short SQLAllocEnv(out IntPtr EnvironmentHandle);

    [DllImport("odbc32.dll")]
    internal static extern short SQLAllocConnect(
      IntPtr EnvironmentHandle,
      out IntPtr ConnectionHandle);

    [DllImport("odbc32.dll", EntryPoint = "SQLDriverConnectW", CharSet = CharSet.Unicode)]
    internal static extern short SQLDriverConnect(
      IntPtr hdbc,
      IntPtr hwnd,
      string szConnStrIn,
      short cbConnStrIn,
      StringBuilder szConnStrOut,
      short cbConnStrOutMax,
      out short pcbConnStrOut,
      ushort fDriverCompletion);

    [DllImport("odbc32.dll")]
    internal static extern short SQLDisconnect(IntPtr ConnectionHandle);

    [DllImport("odbc32.dll")]
    internal static extern short SQLFreeConnect(IntPtr ConnectionHandle);

    [DllImport("odbc32.dll")]
    internal static extern short SQLFreeEnv(IntPtr EnvironmentHandle);

    [DllImport("odbccp32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool SQLGetInstalledDrivers(
      char[] lpszBuf,
      int cbBufMax,
      ref int pcbBufOut);

    [DllImport("odbccp32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SQLGetPrivateProfileString(
      string lpszSection,
      string lpszEntry,
      string lpszDefault,
      StringBuilder RetBuffer,
      int cbRetBuffer,
      string lpszFilename);

    [DllImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWow64Process(IntPtr hProcess, out bool pIsWow64);

    [DllImport("advapi32")]
    internal static extern int RegOpenKeyEx(
      UIntPtr hKey,
      string lpSubKey,
      int ulOptions,
      int samDesired,
      out UIntPtr phkResult);

    [DllImport("advapi32")]
    internal static extern int RegQueryValueEx(
      UIntPtr hKey,
      string lpValueName,
      uint lpReserved,
      ref uint lpType,
      IntPtr lpData,
      ref int lpchData);

    [DllImport("advapi32.dll")]
    internal static extern int RegQueryInfoKey(
      UIntPtr hkey,
      byte[] lpClass,
      IntPtr lpcbClass,
      IntPtr lpReserved,
      out uint lpcSubKeys,
      IntPtr lpcbMaxSubKeyLen,
      IntPtr lpcbMaxClassLen,
      out uint lpcValues,
      IntPtr lpcbMaxValueNameLen,
      IntPtr lpcbMaxValueLen,
      IntPtr lpcbSecurityDescriptor,
      IntPtr lpftLastWriteTime);

    [DllImport("advapi32.dll")]
    internal static extern int RegEnumValue(
      UIntPtr hkey,
      uint index,
      StringBuilder lpValueName,
      ref uint lpcbValueName,
      IntPtr reserved,
      IntPtr lpType,
      IntPtr lpData,
      IntPtr lpcbData);

    [DllImport("advapi32")]
    internal static extern uint RegCloseKey(UIntPtr hKey);

    [Guid("2206CCB1-19C1-11D1-89E0-00C04FD7A829")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDataInitialize
    {
      void GetDataSource(
        [MarshalAs(UnmanagedType.IUnknown), In] object pUnkOuter,
        [MarshalAs(UnmanagedType.U4), In] int dwClsCtx,
        [MarshalAs(UnmanagedType.LPWStr), In] string pwszInitializationString,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown), In, Out] ref object ppDataSource);

      void GetInitializationString(
        [MarshalAs(UnmanagedType.IUnknown), In] object pDataSource,
        [MarshalAs(UnmanagedType.I1), In] bool fIncludePassword,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppwszInitString);

      void Unused_CreateDBInstance();

      void Unused_CreateDBInstanceEx();

      void Unused_LoadStringFromStorage();

      void Unused_WriteStringToStorage();
    }

    [Guid("2206CCB0-19C1-11D1-89E0-00C04FD7A829")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDBPromptInitialize
    {
      void PromptDataSource(
        [MarshalAs(UnmanagedType.IUnknown), In] object pUnkOuter,
        [In] IntPtr hwndParent,
        [MarshalAs(UnmanagedType.U4), In] int dwPromptOptions,
        [MarshalAs(UnmanagedType.U4), In] int cSourceTypeFilter,
        [In] IntPtr rgSourceTypeFilter,
        [MarshalAs(UnmanagedType.LPWStr), In] string pwszszzProviderFilter,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown), In, Out] ref object ppDataSource);

      void Unused_PromptFileName();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class HELPINFO
    {
      public int cbSize = Marshal.SizeOf(typeof (NativeMethods.HELPINFO));
      public int iContextType;
      public int iCtrlId;
      public IntPtr hItemHandle;
      public int dwContextId;
      public NativeMethods.POINT MousePos;
    }

    internal struct POINT
    {
      public int x;
      public int y;
    }
  }
}
