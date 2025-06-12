using System;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.ClassLibrary.Controls
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

		/// <summary>
		/// Event fired when refresh is requested
		/// </summary>
		public event EventHandler RefreshRequested;

		private void RefreshLogButton_Click(object sender, RoutedEventArgs e)
		{
			// Clear the current content
			LogTextBox.Clear();
			// Fire the refresh event so parent controls can reload data
			RefreshRequested?.Invoke(this, EventArgs.Empty);
		}

		public void Clear()
		{
			ControlsHelper.AppInvoke(LogTextBox.Clear);
		}


		public void Add(string format, params object[] args)
		{
			ControlsHelper.AppInvoke(() =>
			{
				var text = (args.Length == 0)
					? format
					: string.Format(format, args);
				LogTextBox.AppendText(text);
			});
		}

	}
}
