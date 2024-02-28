using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginApprovalControl.xaml
	/// </summary>
	public partial class PluginApprovalControl : UserControl, INotifyPropertyChanged
	{
		public PluginApprovalControl()
		{
			_item = emptyData;
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			PluginItemPanel.MethodCheckBox.IsEnabled = false;
		}

		BindingList<PluginApprovalItem> emptyData = new BindingList<PluginApprovalItem>();

		BindingList<PluginApprovalItem> _item;
		public BindingList<PluginApprovalItem> Item
		{
			get => _item;
			set
			{
				if (Equals(value, _item))
					return;
				// Set new item.
				if (_item != null)
				{
					_item.ListChanged -= _item_ListChanged;
				}
				_item = value ?? emptyData;
				DataContext = _item;
				_item.ListChanged += _item_ListChanged;
				OnPropertyChanged(nameof(SowApprovalPanel));
				OnPropertyChanged(nameof(ApprovalItem));
				OnPropertyChanged(nameof(ShowSecondaryAiEvaluation));
				OnPropertyChanged(nameof(FunctionId));
			}
		}

		#region Plugin Approvals

		private void _item_ListChanged(object sender, ListChangedEventArgs e)
		{
			OnPropertyChanged(nameof(SowApprovalPanel));
			OnPropertyChanged(nameof(ApprovalItem));
			OnPropertyChanged(nameof(ShowSecondaryAiEvaluation));
			OnPropertyChanged(nameof(FunctionId));
		}

		public PluginApprovalItem ApprovalItem
			=> Item.FirstOrDefault();

		public Visibility SowApprovalPanel => Item.Count > 0
			? Visibility.Visible : Visibility.Collapsed;

		public Visibility ShowSecondaryAiEvaluation => string.IsNullOrEmpty(ApprovalItem?.SecondaryAiEvaluation)
			? Visibility.Collapsed : Visibility.Visible;

		public string FunctionId => ApprovalItem?.function?.id ?? string.Empty;

		#endregion

		//// It is up to user now to approve.
		//var text = "Do you want to execute function submitted by AI?";
		//		if (!string.IsNullOrEmpty(assistantEvaluation))
		//			text += assistantEvaluation;
		//		text += "\r\n\r\n" + Client.Serialize(function);
		//		var caption = $"{Global.Info.Product} - Plugin Function Approval";

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

		private void ApproveButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ApprovalItem.IsApproved = true;
			ApprovalItem.Semaphore.Release();
		}

		private void DenyButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ApprovalItem.IsApproved = false;
			ApprovalItem.Semaphore.Release();
		}
	}
}
