using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for MainControl.xaml
	/// </summary>
	public partial class MainControl : UserControl, INotifyPropertyChanged
	{
		public MainControl()
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

		public async Task BindData(TemplateItem item, ItemType dataType, Chat.ChatControl chatPanel)
		{
			await Task.Delay(0);
			if (ChatPanel == null)
			{
				// Set chat panel once.
				ChatPanel = chatPanel;
				// Data type will use chat panel
				DataType = dataType;
			}
			if (Equals(item, _Item))
				return;
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
			}
			_Item = item;
			if (_Item != null)
			{
				_Item.PropertyChanged += _item_PropertyChanged;
			}
			OnPropertyChanged(nameof(CreativityName));
			IconPanel.BindData(_Item);
			OnPropertyChanged(nameof(Item));
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TemplateItem.Creativity):
					OnPropertyChanged(nameof(CreativityName));
					break;
				default:
					break;
			}
		}

		private Chat.ChatControl ChatPanel;

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				var binding = new System.Windows.Data.Binding();
				binding.Path = new PropertyPath(nameof(TemplateItemVisibility));
				binding.Source = this;
				ChatPanel.MessagesPanel.DataType = value;
				ChatPanel.MessagePlaceholderTabItem.SetBinding(UIElement.VisibilityProperty, binding);
				// Update the rest.
				OnPropertyChanged(nameof(TemplateItemVisibility));
			}
		}
		private ItemType _DataType;

		#endregion

		#region PanelSettings

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				OnPropertyChanged(nameof(TemplateItemVisibility));
			}
		}

		#endregion

		public Dictionary<reasoning_effort, string> ReasoningEfforts
		{
			get
			{
				if (_ReasoningEfforts == null)
				{
					var values = (reasoning_effort[])Enum.GetValues(typeof(reasoning_effort));
					_ReasoningEfforts = ClassLibrary.Runtime.Attributes.GetDictionary(values);
				}
				return _ReasoningEfforts;
			}
			set => _ReasoningEfforts = value;
		}
		Dictionary<reasoning_effort, string> _ReasoningEfforts;


		public Dictionary<MessageBoxOperation, string> MessageBoxOperations
		{
			get
			{
				if (_MessageBoxOperations == null)
				{
					var values = (MessageBoxOperation[])Enum.GetValues(typeof(MessageBoxOperation));
					_MessageBoxOperations = ClassLibrary.Runtime.Attributes.GetDictionary(values);
				}
				return _MessageBoxOperations;
			}
			set => _MessageBoxOperations = value;
		}
		Dictionary<MessageBoxOperation, string> _MessageBoxOperations;


		public Visibility TemplateItemVisibility
			=> PanelSettings.IsBarPanelVisible && _DataType == ItemType.Template ? Visibility.Visible : Visibility.Collapsed;

		public string CreativityName
		{
			get
			{
				var v = _Item?.Creativity;
				if (v is null)
					return "";
				if (v >= 2 - 0.25)
					return "Very Creative";
				if (v >= 1.5 - 0.25)
					return "Creative";
				if (v >= 1 - 0.25)
					return "Balanced";
				if (v >= 0.5 - 0.25)
					return "Precise";
				return "Very Precise";
			}
		}
		private void HyperLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			ControlsHelper.OpenUrl(e.Uri.AbsoluteUri);
			e.Handled = true;
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

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
