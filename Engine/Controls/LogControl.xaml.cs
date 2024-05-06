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
			LogTextBox.Text = "";
		}

		private void ClearLogButton_Click(object sender, RoutedEventArgs e)
		{
			LogTextBox.Clear();
		}

		public void Clear()
		{
			Dispatcher.Invoke(LogTextBox.Clear);
		}


		public void Add(string format, params object[] args)
		{
			Dispatcher.Invoke(() =>
			{
				var text = (args.Length == 0)
					? format
					: string.Format(format, args);
				LogTextBox.AppendText(text);
			});
		}

	}
}
