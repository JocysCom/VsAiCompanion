using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for LogControl.xaml
	/// </summary>
	public partial class LogControl : UserControl
	{
		public LogControl()
		{
			InitializeComponent();
		}

		private void ClearLogButton_Click(object sender, RoutedEventArgs e)
		{
			LogTextBox.Clear();
		}
	}
}
