using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for RequestDataControl.xaml
	/// </summary>
	public partial class RequestDataControl : UserControl, INotifyPropertyChanged
	{
		public RequestDataControl()
		{
			InitializeComponent();
		}

		#region ■ Properties

		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
		}
		TemplateItem _Item;

		public async Task BindData(TemplateItem item)
		{
			await Task.Delay(0);
			if (Equals(item, _Item))
				return;
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
			}
			_Item = item;
			await RequestHeadersPanel.BindData(_Item?.RequestHeaders);
			await ContentHeadersPanel.BindData(_Item?.RequestContentHeaders);
			await BodyDataPanel.BindData(_Item?.RequestBodyData);
			await QueryDataPanel.BindData(_Item?.RequestQueryData);
			if (_Item != null)
			{
				_Item.PropertyChanged += _item_PropertyChanged;
			}
			OnPropertyChanged(nameof(Item));
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				default:
					break;
			}
		}

		#endregion


		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				RequestHeadersPanel.AddButton.Visibility = System.Windows.Visibility.Collapsed;
				ContentHeadersPanel.AddButton.Visibility = System.Windows.Visibility.Collapsed;
				BodyDataPanel.AddButton.Visibility = System.Windows.Visibility.Collapsed;
				QueryDataPanel.AddButton.Visibility = System.Windows.Visibility.Collapsed;
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
