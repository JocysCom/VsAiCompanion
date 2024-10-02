using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class TaskSettings : NotifyPropertyChanged
	{
		public TaskSettings()
		{
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		[DefaultValue(ItemType.None)]
		public ItemType ItemType { get => _ItemType; set => SetProperty(ref _ItemType, value); }
		private ItemType _ItemType;

		[DefaultValue(0.3)]
		public double GridSplitterPosition
		{
			get => _GridSplitterPosition == 0 ? 0.3 : _GridSplitterPosition;
			set => _GridSplitterPosition = value;
		}
		private double _GridSplitterPosition;

		[DefaultValue(true)]
		public bool IsListPanelVisible { get => _IsListPanelVisible; set => SetProperty(ref _IsListPanelVisible, value); }
		private bool _IsListPanelVisible;

		[DefaultValue(true)]
		public bool IsBarPanelVisible { get => _IsBarPanelVisible; set => SetProperty(ref _IsBarPanelVisible, value); }
		private bool _IsBarPanelVisible;

		//[DefaultValue([])]
		public List<string> ListSelection { get => _ListSelection; set => SetProperty(ref _ListSelection, value); }
		private List<string> _ListSelection;

		public bool ShouldSerializeListSelection() => ListSelection?.Count > 0;

		public List<Guid> GetSelectionListAsGuid() => ListSelection?
			.Select(x => Guid.TryParse(x, out Guid result) ? (Guid?)result : null)
			.Where(x => x != null).Select(x => (Guid)x)
			.ToList();

		/// <summary>
		/// Alternative selection is list selection is not available.
		/// </summary>
		public int ListSelectedIndex { get => _ListSelectedIndex; set => SetProperty(ref _ListSelectedIndex, value); }
		private int _ListSelectedIndex;

		/// <summary>Remember "Search the list" text.</summary>
		[DefaultValue("")]
		public string SearchText { get => _SearchText; set => SetProperty(ref _SearchText, value); }
		private string _SearchText;

		/// <summary>Focused control</summary>
		[DefaultValue(null)]
		public string FocusedControl { get => _FocusedControl; set => SetProperty(ref _FocusedControl, value); }
		private string _FocusedControl;

		/// <summary>Focus</summary>
		[DefaultValue(false)]
		public bool Focus { get => _Focus; set => SetProperty(ref _Focus, value); }
		private bool _Focus;

		/// <summary>
		/// Zoom Settings
		/// </summary>
		[DefaultValue(100)]
		public int ChatPanelZoom { get => _ChatPanelZoom; set => SetProperty(ref _ChatPanelZoom, value); }
		private int _ChatPanelZoom;

		#region Helper Functions

		public void UpdateBarToggleButtonIcon(Button button, bool toggle = false)
		{
			if (toggle)
				IsBarPanelVisible = !IsBarPanelVisible;
			var rt = new RotateTransform();
			rt.Angle = IsBarPanelVisible ? 90 : 270;
			button.RenderTransform = rt;
			button.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		public void UpdateListToggleButtonIcon(Button button, bool toggle = false)
		{
			if (toggle)
				IsListPanelVisible = !IsListPanelVisible;
			var rt = new RotateTransform();
			rt.Angle = IsListPanelVisible ? 0 : 180;
			button.RenderTransform = rt;
			button.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		#endregion

	}
}
