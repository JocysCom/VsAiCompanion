using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// User control
	/// </summary>
	public partial class VisualStudioControl : UserControl, INotifyPropertyChanged
	{
		/// <summary>
		/// Initializes a new instance of the user control class.
		/// </summary>
		public VisualStudioControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			InitMacros();
		}

		/// <summary>
		/// Gets or sets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
		}
		TemplateItem _Item;

		public async Task BindData(TemplateItem item)
		{
			if (Equals(item, _Item))
				return;
			await Task.Delay(0);
			_Item = item;
			OnPropertyChanged(nameof(Item));
		}

		public DataOperation[] AutoOperations
			=> (DataOperation[])Enum.GetValues(typeof(DataOperation));

		public ObservableCollection<CheckBoxViewModel> AttachContexts
		{
			get
			{
				if (_AttachContexts == null)
					_AttachContexts = EnumComboBox.GetItemSource<ContextType>();
				return _AttachContexts;
			}
			set => _AttachContexts = value;
		}
		ObservableCollection<CheckBoxViewModel> _AttachContexts;
		#region Macros

		private void PropertiesRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			InitMacros();
		}

		void InitMacros()
		{
			AddKeys(SelectionComboBox, null, AppHelper.GetReplaceMacrosSelection());
			AddKeys(FileComboBox, null, AppHelper.GetReplaceMacrosDocument());
			AddKeys(DateComboBox, null, AppHelper.GetReplaceMacrosDate());
			AddKeys(VsMacrosComboBox, null, Global.GetEnvironmentProperties());
		}

		int alwaysSelectedIndex = -1;

		void AddKeys(ComboBox cb, string name, IEnumerable<PropertyItem> list)
		{
			var options = new List<PropertyItem>();
			options.AddRange(list);
			cb.ItemsSource = options;
			cb.SelectedIndex = alwaysSelectedIndex;
			cb.SelectionChanged += ComboBox_SelectionChanged;
		}

		public Func<TextBox> GetFocused;

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var cb = (ComboBox)sender;
			if (cb.SelectedIndex <= alwaysSelectedIndex)
				return;
			var item = (PropertyItem)cb.SelectedItem;
			cb.SelectedIndex = alwaysSelectedIndex;
			var box = GetFocused?.Invoke();
			if (box != null)
				AppHelper.InsertText(box, "{" + item.Key + "}");
			// Enable use of macros.
			if (!_Item.UseMacros)
				_Item.UseMacros = true;
		}

		#endregion


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
				var head = "Caring for Your Sensitive Data";
				var body = "As you share files for AI processing, please remember not to include confidential, proprietary, or sensitive information.";
				Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentEnumComboBox, head, body, MessageBoxImage.Warning);
				Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentIcon, head, body, MessageBoxImage.Warning);
				Global.MainControl.InfoPanel.HelpProvider.Add(ContextTypeLabel, head, body, MessageBoxImage.Warning);
				if (!Global.IsVsExtension)
				{
					Global.MainControl.InfoPanel.HelpProvider.Add(FileComboBox, UseMacrosCheckBox.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(SelectionComboBox, UseMacrosCheckBox.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutomationVsLabel, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutoOperationComboBox, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
					Global.MainControl.InfoPanel.HelpProvider.Add(AutoFormatCodeCheckBox, AutomationVsLabel.Content as string, Engine.Resources.MainResources.main_VsExtensionFeatureMessage);
				}
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion


	}
}
