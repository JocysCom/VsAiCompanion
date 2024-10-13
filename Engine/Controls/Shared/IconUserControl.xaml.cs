using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for IconUserControl.xaml
	/// </summary>
	public partial class IconUserControl : UserControl
	{
		public IconUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (IsMouseOver)
				MainGrid_MouseEnter(MainGrid, null);
		}

		ISettingsListFileItem _item;

		public void BindData(ISettingsListFileItem item = null)
		{
			_item = item;
			DataContext = item;
			UpdateButtons();
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog = new System.Windows.Forms.OpenFileDialog();

		public void Edit()
		{
			string contents = null;
			var dialog = _OpenFileDialog;
			dialog.SupportMultiDottedExtensions = true;
			JocysCom.ClassLibrary.Controls.DialogHelper.AddFilter(dialog, ".svg");
			JocysCom.ClassLibrary.Controls.DialogHelper.AddFilter(dialog);
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".svg");
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			contents = System.IO.File.ReadAllText(dialog.FileNames[0]);
			if (string.IsNullOrEmpty(contents))
				_item?.SetIcon(contents);
		}

		public void Copy()
		{
			var item = _item;
			if (item == null || string.IsNullOrEmpty(item.IconData))
				return;
			var contents = SettingsListFileItem.GetContent(item.IconData);
			var tempFolderPath = Path.Combine(AppHelper.GetTempFolderPath(), nameof(Clipboard));
			ClipboardHelper.SetClipboard(item.Name + ".svg", contents, tempFolderPath);
		}

		/// <summary>
		/// Paste icon data from the clipboard to the item.
		/// </summary>
		public void Paste()
		{
			try
			{
				var svg = ClipboardHelper.GetContentFromClipboard(".svg");
				if (!string.IsNullOrEmpty(svg))
					Converters.SvgHelper.LoadSvgFromString(svg);
				_item?.SetIcon(svg);
			}
			catch (Exception ex)
			{
				Global.SetWithTimeout(MessageBoxImage.Error, ex.Message);
			}
		}

		private void IconEditButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var modifiers = Keyboard.Modifiers;
			if (modifiers.HasFlag(ModifierKeys.Alt))
			{
				Paste();
				return;
			}
			if (modifiers.HasFlag(ModifierKeys.Control))
			{
				Copy();
				return;
			}
			Edit();
		}

		private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
		{
			UpdateButtons();
		}

		private void MainGrid_MouseEnter(object sender, MouseEventArgs e)
		{
			InputManager.Current.PreProcessInput += InputManager_PreProcessInput;
			UpdateButtons(true);
		}

		private void MainGrid_MouseLeave(object sender, MouseEventArgs e)
		{
			InputManager.Current.PreProcessInput -= InputManager_PreProcessInput;
			UpdateButtons(true);
		}

		ModifierKeys prevModifiers;

		private void UpdateButtons(bool force = false)
		{
			// Get the current modifiers.
			var modifiers = Keyboard.Modifiers;

			if (prevModifiers == modifiers && !force)
				return;
			prevModifiers = modifiers;

			// Edit button is always visible if icon is not set.
			IconEditButton.Visibility = _item?.Icon == null || MainGrid.IsMouseOver
				? Visibility.Visible
				: Visibility.Hidden;

			// Update the visibility of the icons.
			System.Diagnostics.Debug.WriteLine($"Keyboard.Modifiers: {modifiers}");
			// Check Alt key first, because right Alt will be reported as Alt + Control.
			if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
			{
				// ALT is down - show Paste icon.
				IconEdit.Visibility = Visibility.Collapsed;
				IconCopy.Visibility = Visibility.Collapsed;
				IconPaste.Visibility = Visibility.Visible;
				return;
			}
			if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				// CTRL is down - show Copy icon.
				IconEdit.Visibility = Visibility.Collapsed;
				IconCopy.Visibility = Visibility.Visible;
				IconPaste.Visibility = Visibility.Collapsed;
				return;
			}
			// No modifier - show Edit icon.
			IconEdit.Visibility = Visibility.Visible;
			IconCopy.Visibility = Visibility.Collapsed;
			IconPaste.Visibility = Visibility.Collapsed;
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				//UiPresetsManager.InitControl(this, true);
				UiPresetsManager.AddControls(this);
			}
		}
	}
}
