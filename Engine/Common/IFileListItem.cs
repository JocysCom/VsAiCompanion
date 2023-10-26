using JocysCom.ClassLibrary.Configuration;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public interface IFileListItem : ISettingsItemFile, INotifyPropertyChanged
	{
		bool IsChecked { get; set; }

		string StatusText { get; set; }

		System.Windows.MessageBoxImage StatusCode { get; set; }

		void SetIcon(string contents, string type = ".svg");
	}
}
