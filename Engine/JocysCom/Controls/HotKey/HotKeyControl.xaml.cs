using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.ClassLibrary.Controls.HotKey
{
	/// <summary>
	/// Interaction logic for HotKeyControl.xaml
	/// </summary>
	public partial class HotKeyControl : UserControl
	{

		public HotKeyControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			DataContext = this;
		}

		/// <summary>
		/// Hotkey which will be controlled by this control.
		/// </summary>
		public HotKeyHelper HotKeyHelper { get; set; }

		public bool HotKeyEnabled
		{
			get { return (bool)GetValue(HotKeyEnabledProperty); }
			set { SetValue(HotKeyEnabledProperty, value); }
		}

		public static readonly DependencyProperty HotKeyEnabledProperty =
			DependencyProperty.Register(nameof(HotKeyEnabled), typeof(bool), typeof(HotKeyControl), new PropertyMetadata(false));

		public string HotKeyText
		{
			get { return (string)GetValue(HotKeyTextProperty); }
			set { SetValue(HotKeyTextProperty, value); }
		}

		public static readonly DependencyProperty HotKeyTextProperty =
			DependencyProperty.Register(nameof(HotKeyText), typeof(string), typeof(HotKeyControl), new PropertyMetadata("Enable HotKey"));

		public string HotKey
		{
			get { return (string)GetValue(HotKeyProperty); }
			set
			{
				SetValue(HotKeyProperty, value);
			}
		}

		public static readonly DependencyProperty HotKeyProperty =
			DependencyProperty.Register(nameof(HotKey), typeof(string), typeof(HotKeyControl), new PropertyMetadata(""));

		private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Ignore Tab and Arrow keys to not interfere with UI navigation
			if (e.Key == Key.Tab || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
				return;
			e.Handled = true;
			HotKey = HotKeyHelper.HotKeyToString(Keyboard.Modifiers, e.Key);
		}

		private void TextBox_GotFocus(object sender, RoutedEventArgs e)
		{

			var hk = HotKeyHelper;
			if (hk == null)
				return;
			hk.IsSuspended = true;
			hk.UnregisterHotKey();
		}

		private void TextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			var hk = HotKeyHelper;
			if (hk == null)
				return;
			hk.IsSuspended = false;
			if (HotKeyEnabled)
				hk.RegisterHotKey(HotKey);
		}

	}

}
