using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Interaction logic for PropertySelectControl.xaml
	/// </summary>
	public partial class PropertyComboBox : ComboBox
	{
		public PropertyComboBox()
		{
			InitializeComponent();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}
}
