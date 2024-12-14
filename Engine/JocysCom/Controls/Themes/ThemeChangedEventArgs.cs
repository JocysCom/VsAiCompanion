using System;

namespace JocysCom.ClassLibrary.Controls.Themes
{
	public class ThemeChangedEventArgs : EventArgs
	{
		public ThemeChangedEventArgs(bool useLightTheme)
		{
			UseLightTheme = useLightTheme;
		}

		public bool UseLightTheme { get; }
	}
}
