using System;
using System.Windows;
using System.Windows.Data;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{

	/// <summary>
	/// Converts a string value to asterisks when IsProtected is true.
	/// </summary>
	public class ProtectedValueConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
				return string.Empty;

			string value = values[0] as string;
			bool isProtected = (bool)values[1];

			if (isProtected && !string.IsNullOrEmpty(value))
				return new string('*', value.Length);

			return value;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

}
