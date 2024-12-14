using DocumentFormat.OpenXml.Drawing;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace JocysCom.ClassLibrary.Controls.Themes
{
	public class ThemeHelper
	{
		private static Assembly _themeAssembly;

		/// <summary>
		/// Initializes theme handling and sets the theme based on system settings.
		/// </summary>
		/// <param name="assembly">The assembly where theme resource dictionaries are located.</param>
		public static void InitOnStartup(Assembly assembly)
		{
			_themeAssembly = assembly;
			SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
			SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
			//SwitchAppTheme();
		}

		public static void SwitchAppTheme(ThemeType? theme = ThemeType.Auto)
		{
			//var result = SwitchTheme(Application.Current.Resources, theme);
			var useLightTheme = UseLightTheme(theme);
			OnThemeChanged(useLightTheme);
		}

		// Declare the ThemeChanged event
		public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;

		private static void OnThemeChanged(bool useLightTheme)
		{
			var args = new ThemeChangedEventArgs(useLightTheme);
			ThemeChanged?.Invoke(null, args);
		}

		private static bool UseLightTheme(ThemeType? theme = ThemeType.Auto)
		{
			if (theme == null || theme == ThemeType.Auto)
				return WindowsAppsUseLightTheme();
			return theme == ThemeType.Light;
		}

		public static bool SwitchTheme(ResourceDictionary resources, ThemeType? theme = ThemeType.Auto)
		{
			var useLightTheme = UseLightTheme(theme);

			// Get the merged dictionaries in Application.Resources
			var mergedDictionaries = resources.MergedDictionaries;

			// Find Default.xaml that contains SharedStyles and theme dictionaries
			var defaultDictionary = mergedDictionaries
				.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.EndsWith("Themes/Default.xaml"));

			// Return false is no theme.
			if (defaultDictionary == null)
				return false;

			// Remove existing theme dictionaries
			var existingThemeDictionaries = mergedDictionaries
				.Where(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Default_"))
				.ToList();

			foreach (var dict in existingThemeDictionaries)
				mergedDictionaries.Remove(dict);

			// If using the light theme, no need to add the dark theme dictionary
			if (useLightTheme)
				return true;

			var themeDictionaryUri = GetThemeDictionaryUri(defaultDictionary, "Default_DarkTheme.xaml");
			if (themeDictionaryUri == null)
				return false;
			// Add the new theme dictionary that contains the theme-specific resources
			var newThemeDictionary = new ResourceDictionary()
			{
				Source = themeDictionaryUri
			};
			mergedDictionaries.Add(newThemeDictionary);
			return true;
		}

		private static Uri GetThemeDictionaryUri(ResourceDictionary defaultDictionary, string fileName)
		{
			// Ensure the theme assembly is set
			if (_themeAssembly == null)
				return null;
			// Derive the theme dictionary URI by replacing "Default.xaml" with "Default_DarkTheme.xaml" in the default dictionary's source
			var defaultSource = defaultDictionary.Source.OriginalString;
			var themeOriginalString = defaultSource.Replace("Default.xaml", fileName);
			var assemblyName = _themeAssembly.GetName().Name;
			themeOriginalString = "JocysCom/Controls/Themes/Default_DarkTheme.xaml";
			var themeDictionaryPath = $"/{assemblyName};component/{themeOriginalString}";
			var uri = new Uri(themeDictionaryPath, UriKind.RelativeOrAbsolute);
			return uri;
		}

		private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
		private const string RegistryValueName = "AppsUseLightTheme";

		/// <summary>
		/// Detects if Windows is using light theme for apps.
		/// </summary>
		/// <returns>True if light theme is used; otherwise, false.</returns>
		public static bool WindowsAppsUseLightTheme()
		{
			try
			{
				using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
				{
					var value = key?.GetValue(RegistryValueName);
					return value != null && int.TryParse(value.ToString(), out int useLightTheme) && useLightTheme != 0;
				}
			}
			catch
			{
				// Fallback to light theme in case of an error
				return true;
			}
		}

		/// <summary>
		/// Responds to changes in user preferences.
		/// </summary>
		private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.General)
				SwitchAppTheme();
		}

		/// <summary>
		/// Responds to changes in system parameters, such as high contrast mode.
		/// </summary>
		private static void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(SystemParameters.HighContrast))
			{
				// Handle high contrast mode if necessary
			}
		}
	}
}
