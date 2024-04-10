using System.Windows.Media;

namespace JocysCom.ClassLibrary.Configuration
{
	public interface ISettingsListFileItem : ISettingsFileItem
	{
		bool IsChecked { get; set; }
		string StatusText { get; set; }
		System.Windows.MessageBoxImage StatusCode { get; set; }
		DrawingImage Icon { get; }
		string IconType { get; set; }
		string IconData { get; set; }
		void SetIcon(string contents, string type = ".svg");

		string ListGroupName { get; }
	}
}
