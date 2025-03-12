using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.Themes;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.ClassLibrary.Windows;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Settings;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class SettingsListFileControl : UserControl, INotifyPropertyChanged
	{
		public SettingsListFileControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			//SourceItems = Global.GetSettingItems(DataType);
			// Configure converter.
			var gridFormattingConverter = MainDataGrid.Resources.Values.OfType<Converters.ItemFormattingConverter>().First();
			gridFormattingConverter.ConvertFunction = _MainDataGridFormattingConverter_Convert;
			Global.OnTasksUpdated += Global_OnTasksUpdated;
			UpdateButtons();
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateButtons();

		bool selectionsUpdating = false;
		private void SourceItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			_ = ControlsHelper.BeginInvoke(() =>
			{
				if (e.ListChangedType == ListChangedType.ItemChanged)
				{
					if (!selectionsUpdating && e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.IsChecked))
					{
						selectionsUpdating = true;
						var selectedItems = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().ToList();
						// Get updated item.
						var item = (ISettingsListFileItem)MainDataGrid.Items[e.NewIndex];
						if (selectedItems.Contains(item))
						{
							// Update other items to same value.
							selectedItems.Remove(item);
							foreach (var selecetdItem in selectedItems)
								if (selecetdItem.IsChecked != item.IsChecked)
									selecetdItem.IsChecked = item.IsChecked;
						}
						selectionsUpdating = false;
					}
					bool refreshGrid = false;
					if (!selectionsUpdating)
					{
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.IsPinned))
							refreshGrid = true;
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.ListGroupTimeSortKey))
							refreshGrid = true;
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.ListGroupNameSortKey))
							refreshGrid = true;
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.ListGroupPathSortKey))
							refreshGrid = true;
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.Path) && nameof(ISettingsListFileItem.ListGroupPath) == _GroupingProperty)
							refreshGrid = true;
					}
					if (refreshGrid)
					{
						_ = Helper.Debounce(RefreshDataGrid);
					}
				}
			});
		}

		public void RefreshDataGrid()
		{
			//List<ISettingsListFileItem> items;
			//switch (_GroupingProperty)
			//{
			//	case nameof(SettingsListFileItem.ListGroupTime):
			//		items = FilteredList.OrderBy(x => x.ListGroupTimeSortKey).ToList();
			//		CollectionsHelper.Synchronize(items, FilteredList);
			//		break;
			//	case nameof(SettingsListFileItem.ListGroupPath):
			//		items = FilteredList.OrderBy(x => x.ListGroupPathSortKey).ToList();
			//		CollectionsHelper.Synchronize(items, FilteredList);
			//		break;
			//	case nameof(SettingsListFileItem.ListGroupName):
			//		items = FilteredList.OrderBy(x => x.ListGroupNameSortKey).ToList();
			//		CollectionsHelper.Synchronize(items, FilteredList);
			//		break;
			//	default:
			var view = (ICollectionView)MainDataGrid.ItemsSource;
			view.Refresh();
			//		break;
			//}
		}

		object _MainDataGridFormattingConverter_Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			var sender = (FrameworkElement)values[0];
			var template = (FrameworkElement)values[1];
			var cell = (DataGridCell)(template ?? sender).Parent;
			var value = values[2];
			var item = (ISettingsListFileItem)cell.DataContext;
			// Format ConnectionClassColumn value.
			// Format StatusCodeColumn value.
			if (cell.Column == StatusCodeColumn)
			{
				switch (item.StatusCode)
				{
					case MessageBoxImage.Error:
						return Icons.Current[Icons.Icon_Error];
					case MessageBoxImage.Question:
						return Icons.Current[Icons.Icon_Question];
					case MessageBoxImage.Warning:
						return Icons.Current[Icons.Icon_Warning];
					case MessageBoxImage.Information:
						return Icons.Current[Icons.Icon_Information];
					default:
						return null;
				}
			}
			return value;
		}

		public IBindingList SourceItems { get; set; }

		public SortableBindingList<ISettingsListFileItem> CurrentItems { get; set; } = new SortableBindingList<ISettingsListFileItem>();

		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Get settings.
				if (SettingsData != null)
					SettingsData.FilesChanged -= SettingsData_FilesChanged;
				SettingsData = Global.GetSettings(value);
				if (SettingsData != null)
					SettingsData.FilesChanged += SettingsData_FilesChanged;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update other controls.
				MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				if (SourceItems != null)
					SourceItems.ListChanged -= SourceItems_ListChanged;
				SourceItems = Global.GetSettingItems(value);
				InitSearch();
				// Re-attach events.
				MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				if (SourceItems != null)
					SourceItems.ListChanged += SourceItems_ListChanged;
				var columns = new List<DataGridColumn> { IconColumn, NameColumn };
				var buttons = ControlsHelper.GetAll<Button>(TemplateListGrid).ToList();
				buttons = buttons.Except(new Button[] { GenerateTitleButton, CreateNewTaskButton }).ToList();
				switch (DataType)
				{
					case ItemType.None:
						break;
					case ItemType.Task:
						SetGrouping(nameof(SettingsListFileItem.ListGroupTime));
						buttons.Add(GenerateTitleButton);
						break;
					case ItemType.Template:
						SetGrouping(nameof(SettingsListFileItem.ListGroupName));
						buttons.Add(CreateNewTaskButton);
						break;
					case ItemType.FineTuning:
					case ItemType.Assistant:
					case ItemType.Embeddings:
					case ItemType.UiPreset:
						SetGrouping(nameof(SettingsListFileItem.ListGroupName));
						break;
					case ItemType.Lists:
					case ItemType.MailAccount:
					case ItemType.VaultItem:
					case ItemType.AiService:
					case ItemType.AiModel:
						SetGrouping(nameof(SettingsListFileItem.ListGroupPath));
						columns.Remove(IconColumn);
						break;
					default:
						break;
				}
				ShowColumns(columns.ToArray());
				AppHelper.ShowButtons(TemplateListGrid, buttons.ToArray());
			}
		}
		private ItemType _DataType;

		private void SettingsData_FilesChanged(object sender, EventArgs e)
		{
			ControlsHelper.AppBeginInvoke(() =>
			{
				var sd = sender as ISettingsData;
				if (sd != null && sd == SettingsData)
					// Reload data from the disk.
					SettingsData.Load();
			});
		}

		public void SelectByName(params string[] name)
		{
			ControlsHelper.SetSelection(MainDataGrid, nameof(ISettingsListFileItem.Name), name.ToList(), 0);
		}

		TaskSettings PanelSettings { get; set; } = new TaskSettings();
		ISettingsData SettingsData { get; set; }

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		#endregion

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Debounce(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		private void UpdateOnSelectionChanged()
		{
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(ISettingsListFileItem.Name));
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				// Try to restore selection.
				ControlsHelper.SetSelection(
					MainDataGrid, nameof(ISettingsListFileItem.Name),
					PanelSettings.ListSelection, PanelSettings.ListSelectedIndex
				);
			}
			UpdateButtons();
		}


		void UpdateButtons()
		{
			var allowEnable = true;
			var selecetedItems = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>();
			var isSelected = selecetedItems.Count() > 0;
			if (DataType == ItemType.Template)
			{
				// Count updatable references.
				allowEnable = selecetedItems.Count(x => x.StatusCode == MessageBoxImage.Information) > 0;
			}
			var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
			CreateNewTaskButton.IsEnabled = isSelected;
			GenerateTitleButton.IsEnabled = isSelected;
			ControlsHelper.SetVisible(ClearButton, !string.IsNullOrEmpty(SearchTextBox.Text));
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.MainControl.InfoPanel.Tasks.ListChanged -= Tasks_ListChanged;
			Global.MainControl.InfoPanel.Tasks.ListChanged += Tasks_ListChanged;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			ISettingsListFileItem item = null;
			if (DataType == ItemType.Template || DataType == ItemType.Task)
			{
				var ti = AppHelper.GetNewTemplateItem(true);
				// Treat the new task as a chat; therefore, clear the input box after sending.
				if (DataType == ItemType.Task)
					ti.MessageBoxOperation = MessageBoxOperation.ClearMessage;
				item = ti;
			}
			if (DataType == ItemType.FineTuning)
				item = AppHelper.GetNewFineTuningItem();
			if (DataType == ItemType.Assistant)
				item = AppHelper.GetNewAssistantItem();
			if (DataType == ItemType.Lists)
				item = AppHelper.GetNewListsItem();
			if (DataType == ItemType.UiPreset)
				item = AppHelper.GetNewUiPresetItem();
			if (DataType == ItemType.Embeddings)
				item = AppHelper.GetNewEmbeddingsItem();
			if (DataType == ItemType.MailAccount)
				item = AppHelper.GetNewMailAccount();
			if (DataType == ItemType.VaultItem)
				item = AppHelper.GetNewVaultItem();
			if (DataType == ItemType.AiService)
				item = AppHelper.GetNewAiService();
			if (DataType == ItemType.AiModel)
				item = AppHelper.GetNewAiModel();
			if (item != null)
				InsertItem(item);
		}

		public void InsertItem(ISettingsListFileItem item)
		{
			var position = FindInsertPosition(SourceItems.Cast<ISettingsListFileItem>().ToList(), item);
			// Make sure new item will be selected and focused.
			PanelSettings.ListSelection = new List<string>() { item.Name };
			PanelSettings.ListSelectedIndex = position;
			SourceItems.Insert(position, item);
			SettingsData?.Save();
		}

		private int FindInsertPosition(IList<ISettingsListFileItem> list, ISettingsListFileItem item)
		{
			for (int i = 0; i < list.Count; i++)
				if (string.Compare(
						$"{list[i].Path}/{list[i].Name}",
						$"{item.Path}/{item.Name}",
						StringComparison.Ordinal
					) > 0)
					return i;
			// If not found, insert at the end
			return list.Count;
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			Delete();
		}

		private void Delete()
		{
			var items = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().ToList();
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.Name).ToArray()))
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				var selection = GetItemToSelect(items);
				if (selection.Item != null)
				{
					PanelSettings.ListSelection = new List<string>() { selection.Item.Name };
					PanelSettings.ListSelectedIndex = selection.Position;
				}
				var sd = SettingsData;
				foreach (var item in items)
				{
					if (sd == null)
					{
						SourceItems.Remove(item);
					}
					else
					{
						var error = sd.DeleteItem(item);
						if (!string.IsNullOrEmpty(error))
							Global.ShowError(error);
					}
				}
			});
		}

		/// <summary>
		/// Get items to select after deletion.
		/// </summary>
		public (int Position, ISettingsListFileItem Item) GetItemToSelect(IList<ISettingsListFileItem> itemsToDelete)
		{
			// Get items sorted as in the list.
			var viewSource = (CollectionViewSource)Resources["GroupedData"];
			var view = (ListCollectionView)viewSource.View;
			var sortedFilteredItems = view.OfType<ISettingsListFileItem>().ToList();
			// Get positions of remaining items.
			var deletePositions = itemsToDelete.Select(x => sortedFilteredItems.IndexOf(x));
			if (!deletePositions.Any())
				return (0, null);
			var minDeletePosition = deletePositions.Min();
			var remainPositions = Enumerable.Range(0, sortedFilteredItems.Count()).Except(deletePositions).ToList();
			if (!remainPositions.Any())
				return (0, null);
			var selectedPosition = remainPositions.FirstOrDefault(x => x > minDeletePosition);
			var selectedItem = sortedFilteredItems[selectedPosition];
			return (selectedPosition, selectedItem);
		}

		#region Grid Editing

		private void MainDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var grid = sender as DataGrid;
			if (grid.SelectedItem != null && e.ChangedButton == MouseButton.Left)
				grid.BeginEdit();
		}

		private string _originalValue;

		private void MainDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
		{
			if (e.Column == NameColumn)
			{
				var textBox = e.EditingElement as TextBox;
				textBox.Focus();
				textBox.SelectAll();
				_originalValue = textBox.Text; // Store the original value
			}
		}

		private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
		{
			if (e.EditAction == DataGridEditAction.Cancel) // Check if editing was cancelled
			{
				var textBox = e.EditingElement as TextBox;
				textBox.Text = _originalValue; // Restore the original value
			}
			else if (e.Column == NameColumn)
			{
				var textBox = e.EditingElement as TextBox;
				var newName = textBox.Text.Trim();
				var item = (ISettingsListFileItem)e.Row.Item;
				var settingsData = Global.GetSettings(DataType);
				if (settingsData != null)
				{
					var error = settingsData.RenameItem(item, newName);
					if (string.IsNullOrEmpty(error))
					{
						// If successfully renamed task then...
						if (item is TemplateItem ti && DataType == ItemType.Task && ti.AutoGenerateTitle)
							// Disable auto generation of title.
							ti.AutoGenerateTitle = false;
					}
					else
					{
						MessageBox.Show(error);
						e.Cancel = true;
						return;
					}
				}
			}
		}

		#endregion

		private void CopyButton_Click(object sender, RoutedEventArgs e)
			=> CopySelectedItems();

		private void PasteButton_Click(object sender, RoutedEventArgs e)
		 => PasteItems();

		public void CopySelectedItems()
		{
			var selectedItems = MainDataGrid.SelectedItems.Cast<object>().ToArray();
			if (!selectedItems.Any())
				return;
			var itemType = SourceItems.GetType().GenericTypeArguments[0];
			var names = selectedItems.Cast<ISettingsListFileItem>().Select(x => x.Name).ToArray();
			var items = Array.CreateInstance(itemType, selectedItems.Length);
			Array.Copy(selectedItems, items, items.Length);
			var tempFolderPath = System.IO.Path.Combine(AppHelper.GetTempFolderPath(), nameof(Clipboard));
			var exception = ClipboardHelper.SetXmlSerializable(itemType, selectedItems, names, tempFolderPath);
			if (exception != null)
				Global.ShowError(exception.Message);
		}

		public void PasteItems()
		{
			try
			{
				var itemType = SourceItems.GetType().GenericTypeArguments[0];
				var items = ClipboardHelper.GetXmlSerializable<ISettingsListFileItem>(itemType);
				foreach (var item in items)
				{
					// If items must be unique.
					if (item is IHasGuid guidItem)
					{
						var hasItem = SourceItems.Cast<IHasGuid>().Any(x => x.Id == guidItem.Id);
						if (hasItem)
							guidItem.Id = Guid.NewGuid();
					}
					AppHelper.FixName(item, SourceItems);
					InsertItem(item);
				}
			}
			catch (Exception ex)
			{
				Global.ShowError(ex.Message);
				return;
			}
		}


		/// <summary>
		///  Event is fired when the DataGrid is rendered and its items are loaded,
		///  which means that you can safely select items at this point.
		/// </summary>
		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			NameColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Allow to run once.
			if (ControlsHelper.AllowLoad(MainDataGrid))
			{
				var list = PanelSettings.ListSelection;
				if (list?.Count > 0)
					ControlsHelper.SetSelection(MainDataGrid, nameof(ISettingsListFileItem.Name), list, 0);
				SearchTextBox.Text = PanelSettings.SearchText;
			}
		}

		private void EditButton_Click(object sender, RoutedEventArgs e)
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, NameColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}

		#region Search Filter

		private void InitSearch()
		{
			// Helper functions to extract token content from parent and child items.
			// For a parent TemplateItem.
			Func<TemplateItem, (string name, string body, DateTime? date)> GetTemplateTokenContent = (x) =>
				(x.Name, string.Join(" ", x.Text, x.TextInstructions), x.Modified);
			// For a child MessageItem.
			Func<MessageItem, (string name, string body, DateTime? date)> GetMessageTokenContent = (x) =>
				("", string.Join(" ", x.Body, x.User, x.BodyInstructions), x.Date);

			// For a parent ListInfo.
			Func<ListInfo, (string name, string body, DateTime? date)> GetListInfoTokenContent = (x) =>
				(x.Name, string.Join(" ", x.Description, x.Instructions), x.Modified);

			Func<ListItem, (string name, string body, DateTime? date)> GetListItemTokenContent = (x) =>
				("", string.Join(" ", x.Key, x.Value, x.Comment), null);

			_SearchHelper = new SearchHelper<ISettingsListFileItem>(item =>
			{
				// Retrieve the search query from the textbox.
				string query = SearchTextBox.Text;
				if (string.IsNullOrWhiteSpace(query))
					return true;

				// Tokenize the query.
				var tokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				// Variables for specific filters.
				string nameFilter = null;
				string bodyFilter = null;
				DateTime? dateFilter = null;
				var defaultTerms = new List<string>();

				// Process each token and extract known prefixes.
				foreach (var token in tokens)
				{
					if (token.StartsWith("body:", StringComparison.OrdinalIgnoreCase))
					{
						bodyFilter = token.Substring("body:".Length).Trim();
					}
					else if (token.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
					{
						nameFilter = token.Substring("name:".Length).Trim();
					}
					else if (token.StartsWith("date:", StringComparison.OrdinalIgnoreCase))
					{
						var dateStr = token.Substring("date:".Length).Trim();
						if (DateTime.TryParse(dateStr, out DateTime parsedDate))
							dateFilter = parsedDate;
					}
					else
					{
						defaultTerms.Add(token);
					}
				}

				// Build a unified collection of token records with the same signature.
				IEnumerable<(string name, string body, DateTime? date)> tokenRecords;

				if (item is TemplateItem ti)
				{
					// For TemplateItem, include the parent's token and all child MessageItem tokens.
					var parentToken = GetTemplateTokenContent(ti);
					var childTokens = ti.Messages.Select(m => GetMessageTokenContent(m));
					tokenRecords = new[] { parentToken }.Concat(childTokens);
				}
				else if (item is ListInfo li)
				{
					// For ListInfo, include the parent's token and all child ListItem tokens.
					var parentToken = GetListInfoTokenContent(li);
					var childTokens = li.Items.Select(i => GetListItemTokenContent(i));
					tokenRecords = new[] { parentToken }.Concat(childTokens);
				}
				else
				{
					// Fallback for other types: use the Name property.
					tokenRecords = new[] { (item.Name, item.Name, item.Modified) };
				}

				// Return the overall match result using the unified matching function.
				return MatchTokens(tokenRecords, nameFilter, bodyFilter, dateFilter, defaultTerms);
			},
			null,
			new ObservableCollection<ISettingsListFileItem>());

			_SearchHelper.SetSource(SourceItems);
			_SearchHelper.Synchronized += _SearchHelper_Synchronized;
			FilteredList = _SearchHelper.FilteredList;
			FilteredList.CollectionChanged += FilteredList_CollectionChanged;
			OnPropertyChanged(nameof(FilteredList));
		}

		/// <summary>
		/// Generalized matching function that checks a collection of token records against the provided filters.
		/// </summary>
		private bool MatchTokens(
			IEnumerable<(string name, string body, DateTime? date)> records,
			string nameFilter,
			string bodyFilter,
			DateTime? dateFilter,
			List<string> defaultTerms)
		{
			bool hasName = !string.IsNullOrWhiteSpace(nameFilter);
			bool hasBody = !string.IsNullOrWhiteSpace(bodyFilter);
			bool hasDate = dateFilter.HasValue;
			bool hasDefault = defaultTerms != null && defaultTerms.Any();

			// Check the name filter against the name field.
			if (hasName && !records.Any(r => !string.IsNullOrWhiteSpace(r.name) &&
											   r.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0))
				return false;

			// Check the body filter (also validating the date if provided).
			if (hasBody && !records.Any(r => !string.IsNullOrWhiteSpace(r.body) &&
											   r.body.IndexOf(bodyFilter, StringComparison.OrdinalIgnoreCase) >= 0 &&
											   (!hasDate || (r.date.HasValue && r.date.Value.Date == dateFilter.Value.Date))))
				return false;

			// For cases where only a date filter is provided (or along with a name filter), 
			// ensure at least one record has the matching date.
			if (hasDate && !hasBody && !records.Any(r => r.date.HasValue &&
														  r.date.Value.Date == dateFilter.Value.Date))
				return false;

			// Process free-text default terms.
			if (hasDefault)
			{
				// When no explicit filters are provided, check only the parent's (first record's) name.
				if (!hasName && !hasBody && !hasDate)
				{
					var parentToken = records.First();
					if (!defaultTerms.All(term => !string.IsNullOrWhiteSpace(parentToken.name) &&
													parentToken.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
						return false;
				}
				else // If any prefixed filter exists, search across all token records.
				{
					if (!defaultTerms.All(term => records.Any(r =>
							(!string.IsNullOrWhiteSpace(r.name) && r.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
							(!string.IsNullOrWhiteSpace(r.body) && r.body.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))))
						return false;
				}
			}

			return true;
		}

		private void FilteredList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			bool refreshGrid = true;
			//			e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
			//			e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
			//			e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ||
			//			e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace ||
			//			e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move;
			if (refreshGrid)
				_ = Helper.Debounce(RefreshDataGrid);
		}

		public ObservableCollection<ISettingsListFileItem> FilteredList { get; set; }

		public string ListGroupPropertyName { get; set; }

		private void _SearchHelper_Synchronized(object sender, EventArgs e)
		{
			// Try to restore selection.
			ControlsHelper.SetSelection(
				MainDataGrid, nameof(ISettingsListFileItem.Name),
				PanelSettings.ListSelection, 0
			);
		}

		private SearchHelper<ISettingsListFileItem> _SearchHelper;

		private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			var text = SearchTextBox.Text;
			_SearchHelper.Filter();
			if (PanelSettings.SearchText != text)
				PanelSettings.SearchText = text;
			ControlsHelper.SetVisible(ClearButton, !string.IsNullOrEmpty(text));
		}

		private void ClearButton_Click(object sender, RoutedEventArgs e)
		{
			SearchTextBox.Clear();
		}

		#endregion

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			_SearchHelper.Filter();
		}

		private void GenerateIconButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().ToArray();
			foreach (var item in items)
			{
				// Item type specific code.
				if (DataType == ItemType.Task || DataType == ItemType.Template)
				{
					var ti = (TemplateItem)item;
					var task = ClientHelper.GenerateResult(ti, SettingsSourceManager.TemplateGenerateIconTaskName);
					// Assign task to property to make sure it is not garbage collected.
					ti.GenerateIconTask = task;
				}
			}
		}

		private void GenerateTitleButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().ToArray();
			foreach (var item in items)
			{
				// Item type specific code.
				if (DataType == ItemType.Task || DataType == ItemType.Template)
				{
					var ti = (TemplateItem)item;
					var template = string.IsNullOrEmpty(ti.GenerateTitleTemplate)
						? SettingsSourceManager.TemplateGenerateTitleTaskName
						: ti.GenerateTitleTemplate;
					var task = ClientHelper.GenerateResult(ti, template);
					// Assign task to property to make sure it is not garbage collected.
					ti.GenerateTitleTask = task;
				}
			}
		}

		private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			var grid = (DataGrid)sender;
			if (e.Key == Key.Enter)
			{
				// Commit manually and supress selection of next row.
				if (isEditMode)
				{
					e.Handled = true;
					grid.CommitEdit(DataGridEditingUnit.Cell, true);
					grid.CommitEdit(DataGridEditingUnit.Row, true);
				}
			}
			if (!isEditMode && e.Key == Key.Delete)
				Delete();
		}

		private void CreateNewTaskButton_Click(object sender, RoutedEventArgs e)
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>();
			var selection = new List<string>();
			if (DataType == ItemType.Template)
			{
				foreach (var item in selecetedItems)
				{
					if (item is TemplateItem ti)
					{
						var copy = ti.Copy(true);
						copy.IsPinned = false;
						copy.Created = DateTime.Now;
						copy.Modified = copy.Created;
						Global.InsertItem(copy, ItemType.Task);
						selection.Add(item.Name);
					}
				}
			}
			// Select new task in the tasks list on the [Tasks] tab.
			Global.SelectTask(selection.ToArray());
		}

		private void Global_OnTasksUpdated(object sender, EventArgs e)
		{
			if (DataType != ItemType.Task)
				return;
			ControlsHelper.EnsureTabItemSelected(this);
		}


		#region Context Menu

		private void MainDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			var selectedItems = MainDataGrid.SelectedItems.Cast<object>().ToArray();
			var items = selectedItems.Cast<ISettingsListFileItem>().ToArray();
			if (!items.Any())
				return;
			// If you only want the context menu when a row is clicked:
			var dataGrid = (DataGrid)sender;
			// Build your dynamic menu:
			var menu = CreateContextMenuForElement(DataType, items);
			// Assign it to the DataGrid.
			dataGrid.ContextMenu = menu;
			// Force it to open right now:
			menu.PlacementTarget = dataGrid; // or row, whichever you prefer
			menu.IsOpen = true;
			// Mark as handled so WPF does not try to open any other context menus.
			e.Handled = true;
		}

		public ContextMenu CreateContextMenuForElement(ItemType itemType, params ISettingsFileItem[] items)
		{
			var contextMenu = new ContextMenu();
			// Create head item.
			var header = items.Length > 1 ? $"{items.Length} {DataType} Items" : items[0].Name;
			var head = new MenuItem() { Header = header, IsEnabled = false };
			contextMenu.Items.Add(head);
			// Copy
			var copyMenuItem = new MenuItem { Header = "Copy" };
			copyMenuItem.Click += (s, e)
				=> CopySelectedItems();
			contextMenu.Items.Add(copyMenuItem);
			// Paste
			var pasteMenuItem = new MenuItem { Header = "Paste" };
			pasteMenuItem.Click += (s, e)
				=> PasteItems();
			contextMenu.Items.Add(pasteMenuItem);
			// Add "Copy Path(s)" menu item:
			var copyPathMenuItem = new MenuItem { Header = items.Length > 1 ? "Copy Paths" : "Copy Path" };
			var paths = items.Select(x => $"/{itemType}/{x.Name}");
			copyPathMenuItem.Click += (s, e) =>
				System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, paths));
			contextMenu.Items.Add(copyPathMenuItem);

			// Add reset item menu
			var resetMenuItem = new MenuItem { Header = items.Length > 1 ? "Reset Items" : "Reset Item" };
			resetMenuItem.Click += (s, e) =>
			{
				var selectedNames = items.Select(x => x.Name).ToArray();
				foreach (var item in items)
				{
					var error = SettingsSourceManager.ResetItem(itemType, item);
					if (!string.IsNullOrEmpty(error))
						Global.MainControl.InfoPanel.SetBodyError(error);
				}
				SelectByName(selectedNames);
			};
			contextMenu.Items.Add(resetMenuItem);

			// Add reset menus if available.
			var allResetItems = Global.Resets.Items.FirstOrDefault()?.Items;
			if (allResetItems == null)
				return contextMenu;
			var presetMenuItem = new MenuItem { Header = "Update Instructions" };
			contextMenu.Items.Add(presetMenuItem);
			var instructions = (Settings.UpdateInstruction[])Enum.GetValues(typeof(Settings.UpdateInstruction));
			var resetItems = allResetItems.Where(x => paths.Contains(x.Key)).ToArray();
			foreach (var instruction in instructions)
			{
				// Check if item have this instruction.
				var itemsWithInstructionCount = resetItems.Where(x => x.Value == instruction.ToString()).Count();
				var isChecked = itemsWithInstructionCount > 0;
				var menuHeader = Attributes.GetDescription(instruction);
				if (itemsWithInstructionCount > 0)
					menuHeader += $" ({itemsWithInstructionCount})";
				var menuItem = new MenuItem { Header = menuHeader, IsCheckable = true, IsChecked = isChecked };
				menuItem.Checked += (s, e) =>
				{
					// Process each path.
					foreach (var path in paths)
					{
						var resetItem = resetItems.FirstOrDefault(x => x.Key == path);
						// Add or update instruction.
						if (resetItem != null)
						{
							if (instruction == Settings.UpdateInstruction.None)
							{
								allResetItems.Remove(resetItem);
							}
							else
							{
								resetItem.Value = instruction.ToString();
							}
						}
						else if (instruction != Settings.UpdateInstruction.None)
						{
							resetItem = new ListItem { Key = path, Value = instruction.ToString() };
							allResetItems.Add(resetItem);
						}
					}

				};
				menuItem.Unchecked += (s, e) =>
				{
					foreach (var item in resetItems)
						allResetItems.Remove(item);
				};
				presetMenuItem.Items.Add(menuItem);
			}
			return contextMenu;
		}

		#endregion

		#region Grouping

		private string _GroupingProperty;

		public void SetGrouping(string groupingProperty)
		{
			_GroupingProperty = groupingProperty;
			var view = (ICollectionView)MainDataGrid.ItemsSource;
			view.GroupDescriptions.Clear();
			view.SortDescriptions.Clear();
			if (groupingProperty == null)
				return;
			switch (groupingProperty)
			{
				case nameof(SettingsListFileItem.ListGroupTime):
					view.SortDescriptions.Add(new SortDescription(nameof(SettingsListFileItem.ListGroupTimeSortKey), ListSortDirection.Ascending));
					break;
				case nameof(SettingsListFileItem.ListGroupPath):
					view.SortDescriptions.Add(new SortDescription(nameof(SettingsListFileItem.ListGroupPathSortKey), ListSortDirection.Ascending));
					break;
				case nameof(SettingsListFileItem.ListGroupName):
					view.SortDescriptions.Add(new SortDescription(nameof(SettingsListFileItem.ListGroupNameSortKey), ListSortDirection.Ascending));
					break;
				default:
					view.SortDescriptions.Add(new SortDescription(groupingProperty, ListSortDirection.Ascending));
					break;
			}
			view.SortDescriptions.Add(new SortDescription(nameof(SettingsListFileItem.Name), ListSortDirection.Ascending));
			view.GroupDescriptions.Add(new PropertyGroupDescription(groupingProperty));
			_ = Helper.Debounce(RefreshDataGrid);
		}

		private void ExpanderToggle_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			if (button == null)
				return;
			var expander = FindParent<Expander>(button);
			if (expander != null)
				expander.IsExpanded = !expander.IsExpanded;
		}

		public static T FindParent<T>(DependencyObject child) where T : DependencyObject
		{
			var parentObject = VisualTreeHelper.GetParent(child);
			if (parentObject == null)
				return null;
			T parent = parentObject as T;
			return parent == null
				? FindParent<T>(parentObject)
				: parent;
		}

		#endregion


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}

}

