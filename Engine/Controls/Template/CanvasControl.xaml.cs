using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Windows;
using JocysCom.VS.AiCompanion.Engine.Controls.Shared;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Represents a user control that allows the user to enable or disable a canvas panel
	/// and select target controls within other windows.
	/// </summary>
	public partial class CanvasControl : UserControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CanvasControl"/> class.
		/// </summary>
		public CanvasControl()
		{
			InitializeComponent();
			// Subscribe to the TargetSelected event from the TargetButtonControl.
			TargetPanel.TargetSelected += TargetPanel_TargetSelected;
		}

		// Create an instance of the CanvasHelper.
		private readonly CanvasHelper _CanvasHelper = new CanvasHelper();

		private TemplateItem _Item;
		/// <summary>
		/// Gets or sets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				_Item = value;
				DataContext = value;
				// Additional logic can be added here to handle changes to the Item property.
			}
		}

		/// <summary>
		/// Handles the TargetSelected event from the TargetButtonControl.
		/// Displays information about the selected control or window.
		/// </summary>
		private void TargetPanel_TargetSelected(object sender, TargetSelectedEventArgs e)
		{
			AutomationElement windowElement = e.WindowElement;
			AutomationElement controlElement = e.ControlElement;
			var item = Item;
			if (item != null)
			{
				var path = AutomationHelper.GetPath(controlElement);
				item.CanvasEditorElementPath = path;
			}
		}

		/// <summary>
		/// Handles the Loaded event of the user control.
		/// Initializes help content and UI presets when the control is loaded.
		/// </summary>
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		/// <summary>
		/// Handles the Unloaded event of the user control.
		/// Add any necessary cleanup logic here.
		/// </summary>
		private void This_Unloaded(object sender, RoutedEventArgs e)
		{
			// Cleanup logic can be added here if necessary.
		}
	}
}
