using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.ClassLibrary.Controls
{
	/// <summary>
	/// Interaction logic for PlaceholderTextBox.xaml
	/// </summary>
	public partial class PlaceholderTextBox : UserControl
	{
		public PlaceholderTextBox()
		{
			InitializeComponent();
		}

		#region Events

		public event TextChangedEventHandler TextChanged
		{
			add { PART_ContentTextBox.TextChanged += value; }
			remove { PART_ContentTextBox.TextChanged -= value; }
		}

		public new event KeyEventHandler PreviewKeyUp
		{
			add { PART_ContentTextBox.PreviewKeyUp += value; }
			remove { PART_ContentTextBox.PreviewKeyUp -= value; }
		}

		public new event KeyEventHandler PreviewKeyDown
		{
			add { PART_ContentTextBox.PreviewKeyDown += value; }
			remove { PART_ContentTextBox.PreviewKeyDown -= value; }
		}

		#endregion

		#region Text DependencyProperty

		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register(
				nameof(Text),
				typeof(string),
				typeof(PlaceholderTextBox),
				new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public string Text
		{
			get => (string)GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		#endregion

		#region DependencyProperty: PlaceholderText

		public static readonly DependencyProperty PlaceholderTextProperty =
			DependencyProperty.Register(
				nameof(PlaceholderText),
				typeof(string),
				typeof(PlaceholderTextBox),
				new PropertyMetadata(""));

		public string PlaceholderText
		{
			get => (string)GetValue(PlaceholderTextProperty);
			set => SetValue(PlaceholderTextProperty, value);
		}

		#endregion

		#region DependencyProperty: ShowPlaceholderOnFocusProperty

		public static readonly DependencyProperty ShowPlaceholderOnFocusProperty =
			DependencyProperty.Register(
				nameof(ShowPlaceholderOnFocus),
				typeof(bool),
				typeof(PlaceholderTextBox),
				new PropertyMetadata(true));

		public bool ShowPlaceholderOnFocus
		{
			get => (bool)GetValue(ShowPlaceholderOnFocusProperty);
			set => SetValue(ShowPlaceholderOnFocusProperty, value);
		}

		#endregion
	}
}
