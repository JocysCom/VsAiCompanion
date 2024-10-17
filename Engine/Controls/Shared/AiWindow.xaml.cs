using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for AI Window.
	/// </summary>
	public partial class AiWindow : Window
	{

		public AiWindowInfo Info { get; set; }

		public AiWindow()
		{
			InitializeComponent();
		}

		private void SendButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			This.Close();
		}

		private void DataTextBox_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
		{
		}

		private void DataTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{

		}

		private void DataTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{

		}

		#region Properties

		public static readonly DependencyProperty AttachSelectionProperty =
			DependencyProperty.Register(nameof(AttachSelection), typeof(bool), typeof(AiWindow), new PropertyMetadata(false));

		public bool AttachSelection
		{
			get { return (bool)GetValue(AttachSelectionProperty); }
			set { SetValue(AttachSelectionProperty, value); }
		}

		public static readonly DependencyProperty AttachDocumentProperty =
			DependencyProperty.Register(nameof(AttachDocument), typeof(bool), typeof(AiWindow), new PropertyMetadata(false));

		public bool AttachDocument
		{
			get { return (bool)GetValue(AttachDocumentProperty); }
			set { SetValue(AttachDocumentProperty, value); }
		}


		#endregion
	}
}
