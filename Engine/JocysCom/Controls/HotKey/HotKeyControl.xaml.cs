using System.Collections.Generic;
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
			set { SetValue(HotKeyProperty, value); }
		}

		public static readonly DependencyProperty HotKeyProperty =
			DependencyProperty.Register(nameof(HotKey), typeof(string), typeof(HotKeyControl), new PropertyMetadata(""));

		private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Ignore Tab and Arrow keys to not interfere with UI navigation
			if (e.Key == Key.Tab || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
				return;

			e.Handled = true;

			// Capture modifier keys
			var modifiers = ModifierKeys.None;
			var keys = new List<string>();
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				keys.Add("CTRL");
				modifiers |= ModifierKeys.Control;
			}
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
			{
				keys.Add("ALT");
				modifiers |= ModifierKeys.Alt;
			}
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				keys.Add("SHIFT");
				modifiers |= ModifierKeys.Shift;
			}
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
			{
				keys.Add("WIN");
				modifiers |= ModifierKeys.Windows;
			}
			keys.Add(e.Key.ToString());
			HotKey = string.Join("+", keys);
			// Register the hotkey if enabled
			if (HotKeyEnabled && ParentWindow != null)
				RegisterHotKey(modifiers, e.Key);
		}

		private Window _parentWindow;
		private HotKeyHelper _hotKeyHelper;

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
			_hotKeyHelper?.Dispose();
			_hotKeyHelper = new HotKeyHelper(ParentWindow);
			_hotKeyHelper.HotKeyPressed += HotKeyHelper_HotKeyPressed;
			_hotKeyHelper.RegisterHotKey(modifiers, key);
		}

		private void HotKeyHelper_HotKeyPressed(object sender, System.EventArgs e)
		{
			// Handle hotkey pressed event
			//MessageBox.Show("HotKey Pressed: " + HotKey);
		}

		// Clean up when unloaded
		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			_hotKeyHelper?.Dispose();
		}
	}

}
