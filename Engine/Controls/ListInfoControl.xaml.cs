using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction User Control.
	/// </summary>
	public partial class ListInfoControl : UserControl, INotifyPropertyChanged
	{
		public ListInfoControl()
		{
			InitializeComponent();
			var statuses = new List<ProgressStatus?>() { null };
			var values = Enum.GetValues(typeof(ProgressStatus)).Cast<ProgressStatus>().Select(x => (ProgressStatus?)x);
			statuses.AddRange(values);
			StatusColumn.ItemsSource = statuses;
			UpdateButtons();
			Global.Tasks.Items.ListChanged += Items_ListChanged;
			RefreshPaths();
		}

		private void Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.PropertyDescriptor?.Name == nameof(TemplateItem.Name))
			{
				_ = Helper.Debounce(RefreshPaths);
			}
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				//PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				//OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion

		#region ■ Property: Item

		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		public ListInfo Item
		{
			get => _Item;
		}
		ListInfo _Item;

		public async Task BindData(ListInfo value)
		{
			await Task.Delay(0);
			if (_Item != null)
			{
				_Item.PropertyChanged -= _Item_PropertyChanged;
			}
			_Item = value;
			if (value != null)
			{
				//var path = Global.GetPath(value);
				//if (!Directory.Exists(path))
				//	Directory.CreateDirectory(path);
				_Item.PropertyChanged += _Item_PropertyChanged;
				ControlsHelper.SetItemsSource(MainDataGrid, null);
			}
			IconPanel.BindData(value);
			DataContext = value;
			ControlsHelper.SetItemsSource(MainDataGrid, value?.Items);
			//AiModelBoxPanel.BindData(value);
			//IconPanel.BindData(value);
			// SourceFilesPanel.Data = value;
			//TuningFilesPanel.Data = value;
			//RemoteFilesPanel.Data = value;
			//TuningJobsListPanel.Data = value;
			//ModelsPanel.Data = value;
			//OnPropertyChanged(nameof(DataFolderPath));
			OnPropertyChanged(nameof(Item));
		}


		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				default:
					break;
			}
		}

		#endregion

		#region ■ Property: ListToggleVisibility

		/// <summary>
		/// Indicates whether the button used to toggle list visibility is shown.
		/// </summary>
		public static readonly DependencyProperty ListToggleVisibilityProperty =
			DependencyProperty.Register(
				nameof(ListToggleVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible));

		/// <summary>
		/// Gets or sets a value controlling the visibility of
		/// the ListToggleButton in the top-left corner.
		/// </summary>
		public Visibility ListToggleVisibility
		{
			get => (Visibility)GetValue(ListToggleVisibilityProperty);
			set => SetValue(ListToggleVisibilityProperty, value);
		}

		#endregion

		#region ■ Property: FeatureDescriptionVisibility

		/// <summary>Show Feature Description</summary>
		public static readonly DependencyProperty FeatureDescriptionVisibilityProperty =
			DependencyProperty.Register(
				nameof(FeatureDescriptionVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible));

		/// <summary>Show Feature Description</summary>
		public Visibility FeatureDescriptionVisibility
		{
			get => (Visibility)GetValue(FeatureDescriptionVisibilityProperty);
			set => SetValue(FeatureDescriptionVisibilityProperty, value);
		}

		#endregion


		#region ■ Property: OptionsVisibility

		/// <summary>
		/// Indicates whether the path label and associated options stack are visible.
		/// </summary>
		public static readonly DependencyProperty OptionsVisibilityProperty =
			DependencyProperty.Register(
				nameof(OptionsVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible, OnOptionsVisibilityChanged));

		/// <summary>
		/// Gets or sets a value controlling the visibility of
		/// the path label and StackPanel on the same row.
		/// </summary>
		public Visibility OptionsVisibility
		{
			get => (Visibility)GetValue(OptionsVisibilityProperty);
			set => SetValue(OptionsVisibilityProperty, value);
		}

		private static void OnOptionsVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ListInfoControl ctrl && e.NewValue is Visibility visibility)
			{
			}
		}

		#endregion

		#region ■ Property: IconVisibility

		/// <summary>
		/// Indicates whether the path label and associated Icon stack are visible.
		/// </summary>
		public static readonly DependencyProperty IconVisibilityProperty =
			DependencyProperty.Register(
				nameof(IconVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible, OnIconVisibilityChanged));

		/// <summary>
		/// Gets or sets a value controlling the visibility of
		/// the path label and StackPanel on the same row.
		/// </summary>
		public Visibility IconVisibility
		{
			get => (Visibility)GetValue(IconVisibilityProperty);
			set => SetValue(IconVisibilityProperty, value);
		}

		private static void OnIconVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ListInfoControl ctrl && e.NewValue is Visibility visibility)
			{
			}
		}

		#endregion

		#region ■ Property: FeatureDescription

		/// <summary>
		/// Brief textual summary shown at the top of this control.
		/// </summary>
		public static readonly DependencyProperty FeatureDescriptionProperty =
			DependencyProperty.Register(
				nameof(FeatureDescription),
				typeof(string),
				typeof(ListInfoControl),
				new PropertyMetadata(string.Empty, OnFeatureDescriptionChanged));

		/// <summary>
		/// Gets or sets the text displayed by the FeatureDescriptionLabel.
		/// </summary>
		public string FeatureDescription
		{
			get => (string)GetValue(FeatureDescriptionProperty);
			set => SetValue(FeatureDescriptionProperty, value);
		}

		private static void OnFeatureDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ListInfoControl ctrl && e.NewValue is string newText)
			{

			}
		}

		#endregion

		#region ■ Property: DescriptionVisibility

		/// <summary>
		/// Indicates whether the description label and textbox are visible.
		/// </summary>
		public static readonly DependencyProperty DescriptionVisibilityProperty =
			DependencyProperty.Register(
				nameof(DescriptionVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible, OnDescriptionVisibilityChanged));

		/// <summary>
		/// Gets or sets a value controlling the visibility of
		/// the description label and textbox.
		/// </summary>
		public Visibility DescriptionVisibility
		{
			get => (Visibility)GetValue(DescriptionVisibilityProperty);
			set => SetValue(DescriptionVisibilityProperty, value);
		}

		private static void OnDescriptionVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ListInfoControl ctrl && e.NewValue is Visibility visibility)
			{
			}
		}

		#endregion

		#region ■ Property: InstructionsVisibility

		/// <summary>
		/// Indicates whether the instructions label and textbox are visible.
		/// </summary>
		public static readonly DependencyProperty InstructionsVisibilityProperty =
			DependencyProperty.Register(
				nameof(InstructionsVisibility),
				typeof(Visibility),
				typeof(ListInfoControl),
				new PropertyMetadata(Visibility.Visible, OnInstructionsVisibilityChanged));

		/// <summary>
		/// Gets or sets a value controlling the visibility of
		/// the instructions label and textbox.
		/// </summary>
		public Visibility InstructionsVisibility
		{
			get => (Visibility)GetValue(InstructionsVisibilityProperty);
			set => SetValue(InstructionsVisibilityProperty, value);
		}

		private static void OnInstructionsVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ListInfoControl ctrl && e.NewValue is Visibility visibility)
			{
			}
		}

		#endregion

		#region ■ Property: Column Comment

		public static readonly DependencyProperty ValueTypeProperty =
		DependencyProperty.Register(
			nameof(ValueType), typeof(Type), typeof(ListInfoControl),
			new PropertyMetadata(typeof(string), OnValueTypeChanged));

		public Type ValueType
		{
			get => (Type)GetValue(ValueTypeProperty);
			set => SetValue(ValueTypeProperty, value);
		}

		private static void OnValueTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (ListInfoControl)d;
			ctrl.UpdateValueColumn();
		}

		private void UpdateValueColumn()
		{
			// Remove any existing “Value” column from the DataGrid.
			var dgColumn = MainDataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Value");
			if (dgColumn != null)
				MainDataGrid.Columns.Remove(dgColumn);

			if (ValueType != null && ValueType.IsEnum)
			{
				// If your property is ProgressStatus? (nullable enum):
				// Build a list that includes null + all possible enum values.
				var enumList = new List<object>();
				if (Nullable.GetUnderlyingType(ValueType) != null)
				{
					// Add null (for the “no value” case):
					enumList.Add(null);
					var coreEnum = Nullable.GetUnderlyingType(ValueType);
					if (coreEnum != null)
						enumList.AddRange(Enum.GetValues(coreEnum).Cast<object>());
				}
				else
				{
					// If not nullable, just add all enum values.
					enumList.AddRange(Enum.GetValues(ValueType).Cast<object>());
				}

				var comboColumn = new DataGridComboBoxColumn
				{
					Header = "Value",
					SelectedValueBinding = new Binding("Value")
					{
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
					},
					SelectedValuePath = ".",
					Width = DataGridLength.Auto
				};

				//// Create a DataGridCell style to center content horizontally (in display mode).
				//var cellStyle = new Style(typeof(DataGridCell));
				//cellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
				//cellStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(3, 0, 3, 0)));
				//comboColumn.CellStyle = cellStyle;

				//// Create an EditingElementStyle for the ComboBox's edit mode.
				//var editingComboStyle = new Style(typeof(ComboBox));
				//editingComboStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
				//editingComboStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(3, 0, 3, 0)));
				//comboColumn.EditingElementStyle = editingComboStyle;

				// Add enum values to the ItemsSource. For nullable, include 'null' entry if needed.
				var enumItems = new List<object>(Enum.GetValues(ValueType).Cast<object>());
				comboColumn.ItemsSource = enumItems;

				// Finally, add the column to your DataGrid.
				MainDataGrid.Columns.Add(comboColumn);
			}
			else
			{
				// Fallback to a Text column for non-enum cases.
				var textCol = new DataGridTextColumn
				{
					Header = dgColumn?.Header ?? "Value",
					Binding = new Binding("Value")
					{
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
					},
					Width = DataGridLength.Auto
				};
				MainDataGrid.Columns.Add(textCol);
			}
		}

		#endregion

		#region ■ Property: Column Status Visibility

		/// <summary>Controls column visibility.</summary>
		public static readonly DependencyProperty ColumnStatusVisibilityProperty =
			DependencyProperty.Register(
				nameof(ColumnStatusVisibility), typeof(Visibility),
				typeof(ListInfoControl), new PropertyMetadata(Visibility.Visible));

		/// <summary>Controls column visibility.</summary>
		public Visibility ColumnStatusVisibility
		{
			get => (Visibility)GetValue(ColumnStatusVisibilityProperty);
			set => SetValue(ColumnStatusVisibilityProperty, value);
		}

		#endregion

		#region ■ Property: Column Comment Visibility

		/// <summary>Controls column visibility.</summary>
		public static readonly DependencyProperty ColumnCommentVisibilityProperty =
			DependencyProperty.Register(
				nameof(ColumnCommentVisibility), typeof(Visibility),
				typeof(ListInfoControl), new PropertyMetadata(Visibility.Visible));

		/// <summary>Controls column visibility.</summary>
		public Visibility ColumnCommentVisibility
		{
			get => (Visibility)GetValue(ColumnCommentVisibilityProperty);
			set => SetValue(ColumnCommentVisibilityProperty, value);
		}

		#endregion

		public ObservableCollection<string> Paths { get; set; } = new ObservableCollection<string>();

		public void RefreshPaths()
		{

			var listPaths = Global.Lists.Items.Select(x => x.Path ?? "")
				.Distinct()
				.OrderBy(x => x)
				.ToList();
			if (!listPaths.Contains(""))
				listPaths.Insert(0, "");
			var taskNames = Global.Tasks.Items.Select(x => x.Name).Except(listPaths);
			listPaths.AddRange(taskNames);
			var paths = Paths;
			JocysCom.ClassLibrary.Collections.CollectionsHelper.Synchronize(listPaths, paths);
		}

		#region MainDataGrid

		private void MainDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			var grid = (DataGrid)sender;
			if (e.Key == Key.Enter)
			{
				// Commit manually and supress selection of next row.
				if (isEditMode)
				{
					// Check if SHIFT+ENTER is pressed within a TextBox being edited
					if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
					{
						if (e.OriginalSource is TextBox textBox)
						{
							// Insert newline at the cursor
							int caretIndex = textBox.CaretIndex;
							textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
							textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
							e.Handled = true; // Prevent DataGrid from committing the edit
						}
					}
					else
					{
						e.Handled = true;
						grid.CommitEdit(DataGridEditingUnit.Cell, true);
						grid.CommitEdit(DataGridEditingUnit.Row, true);
					}
				}
			}
			if (!isEditMode && e.Key == Key.Delete)
				Delete();
		}

		private void AddButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Delete();
		}

		private void Delete()
		{
			var items = MainDataGrid.SelectedItems.Cast<ListItem>().ToList();
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.Key).ToArray()))
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				foreach (var item in items)
					Item.Items.Remove(item);
			});
		}

		private void Edit()
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, KeyColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Edit();
		}

		private void MainDataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			Edit();
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Debounce(UpdateButtons, AppHelper.NavigateDelayMs);
		}

		#endregion

		#region Value Editing

		private void MainDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// 1) Force single-click to enter edit mode for any cell that isn't read-only:
			if (e.OriginalSource is FrameworkElement fe)
			{
				var cell = fe.Parent as DataGridCell;
				if (cell != null && !cell.IsEditing && !cell.IsReadOnly)
				{
					// Set the current cell and begin edit:
					var dataGrid = (DataGrid)sender;
					dataGrid.Focus();
					dataGrid.CurrentCell = new DataGridCellInfo(cell);
					dataGrid.BeginEdit();
					e.Handled = true;
				}
			}
		}

		private void MainDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
		{
			// 2) If it’s the “new item” row, WPF might not fully instantiate the editing element
			//    until editing actually begins. This event ensures the row is created.
			//    (No special code needed here unless you have custom logic for new rows.)
		}

		private void MainDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
		{
			// 3) If it’s a ComboBox column, auto-open the dropdown:
			if (e.Column is DataGridComboBoxColumn)
			{
				// Since the EditingElement may not be ready immediately, we use Dispatcher:
				Dispatcher.BeginInvoke(new Action(() =>
				{
					if (e.EditingElement is ComboBox comboBox)
					{
						comboBox.IsDropDownOpen = true;
					}
				}), DispatcherPriority.Input);
			}
		}

		#endregion

		void UpdateButtons()
		{
			var isSelected = MainDataGrid.SelectedItems.Count > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			// Fast workaround
			//MainDataGrid.ItemsSource = _Item?.Items;
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
			AppHelper.AddHelp(IsEnabledCheckBox, IsEnabledCheckBox.Content as string, Engine.Resources.MainResources.main_List_IsEnabled);
			AppHelper.AddHelp(IsReadOnlyCheckBox, IsReadOnlyCheckBox.Content as string, Engine.Resources.MainResources.main_List_IsReadOnly);
			AppHelper.AddHelp(InstructionsLabel, Engine.Resources.MainResources.main_Instructions, Engine.Resources.MainResources.main_List_Instructions);
			AppHelper.AddHelp(InstructionsTextBox, Engine.Resources.MainResources.main_Instructions, Engine.Resources.MainResources.main_List_Instructions);
			AppHelper.AddHelp(DescriptionLabel, Engine.Resources.MainResources.main_Description, Engine.Resources.MainResources.main_List_Description);
			AppHelper.AddHelp(DescriptionTextBox, Engine.Resources.MainResources.main_Description, Engine.Resources.MainResources.main_List_Description);
		}


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void MainDataGrid_PreparingCellForEdit_1(object sender, DataGridPreparingCellForEditEventArgs e)
		{

		}

		private void MainDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
		{

		}
	}

}
