using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.ConnectionUI
{
    public class OdbcConnectionProperties : AdoDotNetConnectionProperties
    {
        private static List<string> _sqlNativeClientDrivers;

        public OdbcConnectionProperties()
          : base("System.Data.Odbc")
        {
        }

        public override bool IsComplete
        {
            get
            {
                return ConnectionStringBuilder["DSN"] is string && (ConnectionStringBuilder["DSN"] as string).Length != 0 || ConnectionStringBuilder["DRIVER"] is string && (ConnectionStringBuilder["DRIVER"] as string).Length != 0;
            }
        }

        public static List<string> SqlNativeClientDrivers
        {
            get
            {
                if (OdbcConnectionProperties._sqlNativeClientDrivers == null)
                {
                    OdbcConnectionProperties._sqlNativeClientDrivers = new List<string>();
                    foreach (string installedDriver in OdbcConnectionProperties.ManagedSQLGetInstalledDrivers())
                    {
                        if (installedDriver.Contains("Native") && installedDriver.Contains("Client"))
                        {
                            StringBuilder RetBuffer = new StringBuilder(1024);
                            if (NativeMethods.SQLGetPrivateProfileString(installedDriver, "Driver", "", RetBuffer, RetBuffer.Capacity, "ODBCINST.INI") > 0 && RetBuffer.Length > 0)
                            {
                                string str = RetBuffer.ToString();
                                int num = str.LastIndexOf('\\');
                                if (num > 0)
                                    OdbcConnectionProperties._sqlNativeClientDrivers.Add(str.Substring(num + 1).ToUpperInvariant());
                            }
                        }
                    }
                    OdbcConnectionProperties._sqlNativeClientDrivers.Sort();
                }
                return OdbcConnectionProperties._sqlNativeClientDrivers;
            }
        }

        private static List<string> ManagedSQLGetInstalledDrivers()
        {
            char[] chArray = new char[1024];
            int pcbBufOut = 0;
            List<string> installedDrivers = new List<string>();
            bool flag;
            try
            {
                for (flag = NativeMethods.SQLGetInstalledDrivers(chArray, chArray.Length, ref pcbBufOut); flag; flag = NativeMethods.SQLGetInstalledDrivers(chArray, chArray.Length, ref pcbBufOut))
                {
                    if (pcbBufOut > 0)
                    {
                        if (pcbBufOut == chArray.Length - 1)
                        {
                            if ((double)chArray.Length < Math.Pow(2.0, 30.0))
                                chArray = new char[chArray.Length * 2];
                            else
                                break;
                        }
                        else
                            break;
                    }
                    else
                        break;
                }
            }
            catch (Exception)
            {
                flag = false;
            }
            if (flag)
            {
                int startIndex = 0;
                int num = Array.IndexOf<char>(chArray, char.MinValue, startIndex, pcbBufOut - 1);
                while (startIndex < pcbBufOut - 1)
                {
                    installedDrivers.Add(new string(chArray, startIndex, num - startIndex));
                    startIndex = num + 1;
                    num = Array.IndexOf<char>(chArray, char.MinValue, startIndex, pcbBufOut - 1 - num);
                }
            }
            return installedDrivers;
        }
    }
}
