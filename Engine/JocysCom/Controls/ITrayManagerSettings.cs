using System.ComponentModel;
using System.Windows;

namespace JocysCom.ClassLibrary.Controls
{
	public interface ITrayManagerSettings : INotifyPropertyChanged
	{
		bool StartWithWindows { get; }
		WindowState StartWithWindowsState { get; }
		bool MinimizeToTray { get; }
		bool MinimizeOnClose { get; }
	}
}
