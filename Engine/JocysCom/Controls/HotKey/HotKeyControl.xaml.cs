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

		void UpdateHotKey()
		{
			// Register the hotkey if enabled
			//if (HotKeyEnabled && ParentWindow != null)
			//	RegisterHotKey(modifiers, e.Key);
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

		private Window _parentWindow;
		public HotKeyHelper HotKeyHelper;

		private Window ParentWindow
		{
			get
			{
				if (_parentWindow == null)
					_parentWindow = Window.GetWindow(this);
				return _parentWindow;
			}
		}

		private void RegisterHotKey(ModifierKeys modifiers, Key key)
		{
			HotKeyHelper.RegisterHotKey(modifiers, key);
		}

		private void HotKeyHelper_HotKeyPressed(object sender, System.EventArgs e)
		{
			// Handle hotkey pressed event
			//MessageBox.Show("HotKey Pressed: " + HotKey);
		}

		// Clean up when unloaded
		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			//HotKeyHelper?.Dispose();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			HotKeyHelper = new HotKeyHelper(ParentWindow);
			HotKeyHelper.HotKeyPressed += HotKeyHelper_HotKeyPressed;
		}
	}

}
