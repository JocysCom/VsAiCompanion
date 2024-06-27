using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AttachmentsControl.xaml
	/// </summary>
	public partial class AttachmentsControl : UserControl, INotifyPropertyChanged
	{
		public AttachmentsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			CurrentItems = new BindingList<MessageAttachments>();
		}

		private void CurrentItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, UpdateControlVisibility);
		}

		private void UpdateControlVisibility()
		{
			// Only visible during debug for a moment.
			Visibility = CurrentItems?.Any() == true && InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
		}

		// Store temp settings.
		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		private void UpdateOnSelectionChanged()
		{
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(MessageAttachments.Location));
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				// Try to restore selection.
				ControlsHelper.SetSelection(
					MainDataGrid, nameof(MessageAttachments.Location),
					PanelSettings.ListSelection, PanelSettings.ListSelectedIndex
				);
			}
		}

		public BindingList<MessageAttachments> CurrentItems
		{
			get => _CurrentItems;
			set
			{
				if (_CurrentItems != null)
					CurrentItems.ListChanged -= CurrentItems_ListChanged;
				_CurrentItems = value;
				if (_CurrentItems != null)
					CurrentItems.ListChanged += CurrentItems_ListChanged;
				OnPropertyChanged();
				UpdateControlVisibility();
			}
		}
		BindingList<MessageAttachments> _CurrentItems;

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		/// <summary>
		///  Event is fired when the DataGrid is rendered and its items are loaded,
		///  which means that you can safely select items at this point.
		/// </summary>
		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			PanelSettings = Global.AppSettings.GetTaskSettings(ItemType.Attachment);
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
			}
		}

		private async Task Remove()
		{
			var items = MainDataGrid.SelectedItems.Cast<MessageAttachments>().ToList();
			// Use begin invoke or grid update will deadlock on same thread.
			await ControlsHelper.BeginInvoke(() =>
			{
				foreach (var item in items)
					CurrentItems.Remove(item);
				if (CurrentItems.Any())
					MainDataGrid.Focus();
			});
		}

		private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			if (!isEditMode && e.Key == Key.Delete)
				ControlsHelper.AppBeginInvoke(async () => await Remove());
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		public void AddFile()
		{
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.Multiselect = true;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			dialog.Title = "Attach file(s)";
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			foreach (var fileName in dialog.FileNames)
			{
				var item = new MessageAttachments();
				item.Title = System.IO.Path.GetFileName(fileName);
				// For Model Processing
				//item.Type = Plugins.Core.VsFunctions.ContextType.ChatHistory
				item.Location = new Uri(fileName).AbsoluteUri;
				CurrentItems.Add(item);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var button = (Button)sender;
			var location = button.Tag as string;
			var item = CurrentItems.FirstOrDefault(x => x.Location == location);
			CurrentItems.Remove(item);
		}
	}
}
