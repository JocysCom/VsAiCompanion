using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System;

namespace JocysCom.ClassLibrary.Controls.Converters
{
	public class EnumToBooleanConverter : IValueConverter
	{

		/// <summary>
		/// Return true if control value and bound value is the same.
		/// </summary>
		/// <param name="value">bound value</param>
		/// <param name="targetType">Boolean type.</param>
		/// <param name="parameter">Control value.</param>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var parameterString = $"{parameter}";
			if (string.IsNullOrEmpty(parameterString))
				return DependencyProperty.UnsetValue;
			if (!Enum.IsDefined(value.GetType(), value))
				return DependencyProperty.UnsetValue;
			var parameterValue = Enum.Parse(value.GetType(), parameterString);
			return parameterValue.Equals(value);
		}

		/// <summary>
		/// Return true if control value and bound value is the same.
		/// </summary>
		/// <param name="value">bound value</param>
		/// <param name="targetType">Boolean type.</param>
		/// <param name="parameter">Control value.</param>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var parameterString = $"{parameter}";
			return string.IsNullOrEmpty(parameterString)
				? DependencyProperty.UnsetValue
				: Enum.Parse(targetType, parameterString);
		}

	}
}
