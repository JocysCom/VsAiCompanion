using System;
using System.Globalization;

namespace Microsoft.Data.ConnectionUI
{
	internal sealed class SR
	{


		private static CultureInfo Culture => (CultureInfo)null;

		public static string GetString(string name, params object[] args)
		{
			string format = SR_Resources.ResourceManager.GetString(name, SR.Culture);
			if (args == null || args.Length <= 0)
				return format;
			for (int index = 0; index < args.Length; ++index)
			{
				if (args[index] is string str && str.Length > 1024)
					args[index] = (object)(str.Substring(0, 1021) + "...");
			}
			return string.Format((IFormatProvider)CultureInfo.CurrentCulture, format, args);
		}

		public static string GetString(string name)
		{
			return SR_Resources.ResourceManager.GetString(name, SR.Culture);
		}

		public static string GetString(string name, out bool usedFallback)
		{
			usedFallback = false;
			return SR.GetString(name);
		}

		public static object GetObject(string name)
		{
			return SR_Resources.ResourceManager.GetObject(name, SR.Culture);
		}
	}
}
