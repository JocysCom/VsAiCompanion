﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for TemplateControl.xaml
	/// </summary>
	public partial class SettingsControl : UserControl, INotifyPropertyChanged
	{

		public SettingsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.StartPosition.PositionLoaded += StartPosition_PositionLoaded;
			MainGrid.SizeChanged += MainGrid_SizeChanged;
			Global.OnSaveSettings += Global_OnSaveSettings;
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		void UpdateOnSelectionChanged()
		{
			var item = ListPanel.MainDataGrid.SelectedItems.Cast<IFileListItem>().FirstOrDefault();
			if (DataType == ItemType.Task || DataType == ItemType.Template)
			{
				TemplateItemPanel.Item = (TemplateItem)item;
			}
			else if (DataType == ItemType.FineTune)
			{
				FineTuningItemPanel.Item = (FineTuningItem)item;
			}
		}

		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				TemplateItemPanel.Visibility = Visibility.Collapsed;
				FineTuningItemPanel.Visibility = Visibility.Collapsed;
				if (value == ItemType.Task || value == ItemType.Template)
				{
					TemplateItemPanel.DataType = value;
					TemplateItemPanel.Visibility = Visibility.Visible;
				}
				else if (value == ItemType.FineTune)
				{
					FineTuningItemPanel.DataType = value;
					FineTuningItemPanel.Visibility = Visibility.Visible;
				}
				else
					return;
				if (ControlsHelper.IsDesignMode(this))
					return;
				ListPanel.MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update child control types.
				ListPanel.DataType = value;
				ListPanel.MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}
		private ItemType _DataType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsListPanelVisible))
			{
				UpdateGridPosition();
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}

		#endregion

		#region GridSplitter Postion

		private bool _LoadGridPosition = true;

		private void Global_OnSaveSettings(object sender, System.EventArgs e)
		{
			SavePositions();
		}

		/// <summary>
		/// This method will be called twice:
		/// 1. At the beginning when the Window is shown, and
		/// 2. when Global.AppSettings.StartPosition.LoadPosition(this) is called in MainWindow.xaml.cs.
		/// </summary>
		private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.WidthChanged && _LoadGridPosition)
				LoadPositions();
		}

		/// <summary>
		/// Runs when windows size and postion is loaded from settings.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void StartPosition_PositionLoaded(object sender, System.EventArgs e)
		{
			LoadPositions();
		}

		void LoadPositions()
		{
			if (DataType == ItemType.None)
				return;
			lock (MainGrid)
			{
				MainGrid.SizeChanged -= MainGrid_SizeChanged;
				if (UpdateGridPosition())
					_LoadGridPosition = false;
				MainGrid.SizeChanged += MainGrid_SizeChanged;
			}
		}

		bool UpdateGridPosition()
		{
			var position = 0.0;
			if (PanelSettings.IsListPanelVisible)
				position = PanelSettings.GridSplitterPosition;
			return PositionSettings.SetGridSplitterPosition(MainGrid, position, null, true);
		}

		void SavePositions()
		{
			if (DataType == ItemType.None)
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == 0.0)
				return;
			PanelSettings.GridSplitterPosition = position;
		}

		#endregion

		public Visibility ListPanelVisibility
			=> PanelSettings.IsListPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}