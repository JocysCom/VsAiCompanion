using JocysCom.VS.AiCompanion;
using JocysCom.VS.AiCompanion.Engine.Converters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for IconUserControl.xaml
	/// </summary>
	public partial class IconUserControl : UserControl
	{
		public IconUserControl()
		{
			InitializeComponent();
		}


		TemplateItem _item;

		public void BindData(TemplateItem item = null)
		{
			_item = item ?? AppHelper.GetNewTemplateItem();
			DataContext = item;
		}

		private void LoadSvgFromFile(string filePath)
		{
			string svgContent = System.IO.File.ReadAllText(filePath);
			LoadSvgFromString(svgContent);
		}

		private void LoadSvgFromString(string svgContent)
		{
			string html = $"<html><body style='margin:0;padding:0;'>{svgContent}</body></html>";

		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog = new System.Windows.Forms.OpenFileDialog();

		public void Edit()
		{
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
			var contents = System.IO.File.ReadAllText(dialog.FileNames[0]);
			_item.SetIcon(contents);
		}

		private void UserControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			//Edit();
		}

		private void IconEditButton_Click(object sender, RoutedEventArgs e)
		{
			Edit();
		}

		private void Grid_MouseEnter(object sender, MouseEventArgs e)
		{
			IconEditButton.Visibility = Visibility.Visible;
		}

		private void Grid_MouseLeave(object sender, MouseEventArgs e)
		{
			IconEditButton.Visibility = Visibility.Hidden;
		}
	}
}
