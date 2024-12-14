using System;
using System.Windows;

namespace JocysCom.ClassLibrary.Controls.Themes
{

	/* Example
	 <UserControl x:Class="YourNamespace.YourUserControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:local="clr-namespace:YourNamespace"
             xmlns:themes="clr-namespace:JocysCom.ClassLibrary.Controls.Themes"
             themes:ThemeBehavior.ApplyTheme="True">
    <!-- Your existing XAML content -->
	</UserControl>
	*/

	/// <summary>
	/// 
	/// </summary>

	public static class ThemeBehavior
	{
		public static bool GetApplyTheme(DependencyObject obj)
		{
			return (bool)obj.GetValue(ApplyThemeProperty);
		}

		public static void SetApplyTheme(DependencyObject obj, bool value)
		{
			obj.SetValue(ApplyThemeProperty, value);
		}

		public static readonly DependencyProperty ApplyThemeProperty =
			DependencyProperty.RegisterAttached(
				"ApplyTheme",
				typeof(bool),
				typeof(ThemeBehavior),
				new PropertyMetadata(false, OnApplyThemeChanged));

		private static void OnApplyThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is FrameworkElement element)
			{
				if ((bool)e.NewValue)
				{
					element.Loaded += Element_Loaded;
					element.Unloaded += Element_Unloaded;

					if (element.IsLoaded)
					{
						Element_Loaded(element, null);
					}
				}
				else
				{
					element.Loaded -= Element_Loaded;
					element.Unloaded -= Element_Unloaded;

					var handler = GetThemeChangedHandler(element);
					if (handler != null)
					{
						ThemeHelper.ThemeChanged -= handler;
						SetThemeChangedHandler(element, null);
					}
				}
			}
		}

		private static void Element_Loaded(object sender, RoutedEventArgs e)
		{
			var element = (FrameworkElement)sender;

			// Apply the current theme to the element's resources
			ThemeHelper.SwitchTheme(element.Resources);

			// Create a handler for theme changes
			EventHandler<ThemeChangedEventArgs> handler = (s, args) =>
			{
				// Ensure the update occurs on the UI thread
				element.Dispatcher.Invoke(() =>
				{
					var theme = args.UseLightTheme
						? ThemeType.Light
						: ThemeType.Dark;
					ThemeHelper.SwitchTheme(element.Resources, theme);
				});
			};

			// Store the handler in an attached property
			SetThemeChangedHandler(element, handler);

			// Subscribe to theme changes
			ThemeHelper.ThemeChanged += handler;
		}

		private static void Element_Unloaded(object sender, RoutedEventArgs e)
		{
			var element = (FrameworkElement)sender;

			// Retrieve and remove the handler
			var handler = GetThemeChangedHandler(element);
			if (handler != null)
			{
				ThemeHelper.ThemeChanged -= handler;
				SetThemeChangedHandler(element, null);
			}
		}

		// Attached property to store the event handler
		private static readonly DependencyProperty ThemeChangedHandlerProperty =
			DependencyProperty.RegisterAttached(
				"ThemeChangedHandler",
				typeof(EventHandler<ThemeChangedEventArgs>),
				typeof(ThemeBehavior),
				new PropertyMetadata(null));

		private static EventHandler<ThemeChangedEventArgs> GetThemeChangedHandler(DependencyObject obj)
		{
			return (EventHandler<ThemeChangedEventArgs>)obj.GetValue(ThemeChangedHandlerProperty);
		}

		private static void SetThemeChangedHandler(DependencyObject obj, EventHandler<ThemeChangedEventArgs> value)
		{
			obj.SetValue(ThemeChangedHandlerProperty, value);
		}
	}
}
