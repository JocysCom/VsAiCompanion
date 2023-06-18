using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace JocysCom.ClassLibrary.Controls.Converters
{
	/*

	<UserControl
		xmlns:JcConverters="clr-namespace:JocysCom.ClassLibrary.Controls.Converters"

	<UserControl.Resources>
		<ResourceDictionary>
			<JcConverters:BoolToVisibilityConverter x:Key="_BoolToVisibilityConverter" />
		</ResourceDictionary>
	</UserControl.Resources>

	<Button Content="My Button" Visibility="{Binding IsButtonVisible, Converter={StaticResource _BoolToVisibilityConverter}}" />


	*/

	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is bool b && b
				? Visibility.Visible
				: Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is Visibility v && v == Visibility.Visible;
		}
	}
}
