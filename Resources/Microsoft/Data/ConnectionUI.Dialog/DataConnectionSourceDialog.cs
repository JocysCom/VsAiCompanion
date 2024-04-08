using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	internal class DataConnectionSourceDialog : Form
	{
		private Label _headerLabel;
		private Dictionary<DataSource, DataProvider> _providerSelections = new Dictionary<DataSource, DataProvider>();
		private DataConnectionDialog _mainDialog;
		private IContainer components;
		private TableLayoutPanel mainTableLayoutPanel;
		private Panel leftPanel;
		private Label dataSourceLabel;
		private ListBox dataSourceListBox;
		private Label dataProviderLabel;
		private ComboBox dataProviderComboBox;
		private GroupBox descriptionGroupBox;
		private Label descriptionLabel;
		private CheckBox saveSelectionCheckBox;
		private TableLayoutPanel buttonsTableLayoutPanel;
		private Button okButton;
		private Button cancelButton;

		public DataConnectionSourceDialog()
		{
			InitializeComponent();
			if (components == null)
				components = (IContainer)new System.ComponentModel.Container();
			components.Add((IComponent)new UserPreferenceChangedHandler((Form)this));
		}

		public DataConnectionSourceDialog(DataConnectionDialog mainDialog)
		  : this()
		{
			_mainDialog = mainDialog;
		}

		public string Title
		{
			get => Text;
			set => Text = value;
		}

		public string HeaderLabel
		{
			get => _headerLabel == null ? string.Empty : _headerLabel.Text;
			set
			{
				if (_headerLabel == null)
				{
					switch (value)
					{
						case null:
							return;
						case "":
							return;
					}
				}
				if (_headerLabel != null && value == _headerLabel.Text)
					return;
				if (value != null)
				{
					if (_headerLabel == null)
					{
						_headerLabel = new Label();
						_headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
						_headerLabel.FlatStyle = FlatStyle.System;
						_headerLabel.Location = new Point(12, 12);
						_headerLabel.Margin = new Padding(3);
						_headerLabel.Name = "dataSourceLabel";
						_headerLabel.Width = mainTableLayoutPanel.Width;
						_headerLabel.TabIndex = 100;
						Controls.Add((Control)_headerLabel);
					}
					_headerLabel.Text = value;
					MinimumSize = Size.Empty;
					_headerLabel.Height = LayoutUtils.GetPreferredLabelHeight(_headerLabel);
					int num = _headerLabel.Bottom + _headerLabel.Margin.Bottom + mainTableLayoutPanel.Margin.Top - mainTableLayoutPanel.Top;
					mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
					Height += num;
					mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
					mainTableLayoutPanel.Top += num;
					MinimumSize = Size;
				}
				else
				{
					if (_headerLabel == null)
						return;
					int height = _headerLabel.Height;
					try
					{
						Controls.Remove((Control)_headerLabel);
					}
					finally
					{
						_headerLabel.Dispose();
						_headerLabel = (Label)null;
					}
					MinimumSize = Size.Empty;
					mainTableLayoutPanel.Top -= height;
					mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
					Height -= height;
					mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
					MinimumSize = Size;
				}
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			if (_mainDialog != null)
			{
				foreach (DataSource dataSource in (IEnumerable<DataSource>)_mainDialog.DataSources)
				{
					if (dataSource != _mainDialog.UnspecifiedDataSource)
						dataSourceListBox.Items.Add((object)dataSource);
				}
				if (_mainDialog.DataSources.Contains(_mainDialog.UnspecifiedDataSource))
				{
					dataSourceListBox.Sorted = false;
					dataSourceListBox.Items.Add((object)_mainDialog.UnspecifiedDataSource);
				}
				int val1 = dataSourceListBox.Width - (dataSourceListBox.Width - dataSourceListBox.ClientSize.Width);
				foreach (object obj in dataSourceListBox.Items)
				{
					Size size = TextRenderer.MeasureText((obj as DataSource).DisplayName, dataSourceListBox.Font);
					size.Width += 3;
					val1 = Math.Max(val1, size.Width);
				}
				Width += (Math.Max(val1 + (dataSourceListBox.Width - dataSourceListBox.ClientSize.Width), dataSourceListBox.MinimumSize.Width) - dataSourceListBox.Size.Width) * 2;
				MinimumSize = Size;
				if (_mainDialog.SelectedDataSource != null)
				{
					dataSourceListBox.SelectedItem = (object)_mainDialog.SelectedDataSource;
					if (_mainDialog.SelectedDataProvider != null)
						dataProviderComboBox.SelectedItem = (object)_mainDialog.SelectedDataProvider;
				}
				foreach (DataSource dataSource in dataSourceListBox.Items)
				{
					DataProvider selectedDataProvider = _mainDialog.GetSelectedDataProvider(dataSource);
					if (selectedDataProvider != null)
						_providerSelections[dataSource] = selectedDataProvider;
				}
			}
			saveSelectionCheckBox.Checked = _mainDialog.SaveSelection;
			SetOkButtonStatus();
			base.OnLoad(e);
		}

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			dataProviderComboBox.Top = leftPanel.Height - leftPanel.Padding.Bottom - dataProviderComboBox.Margin.Bottom - dataProviderComboBox.Height;
			dataProviderLabel.Top = dataProviderComboBox.Top - dataProviderComboBox.Margin.Top - dataProviderLabel.Margin.Bottom - dataProviderLabel.Height;
			int num1 = saveSelectionCheckBox.Right + saveSelectionCheckBox.Margin.Right - (buttonsTableLayoutPanel.Left - buttonsTableLayoutPanel.Margin.Left);
			if (num1 > 0)
			{
				Width += num1;
				MinimumSize = new Size(MinimumSize.Width + num1, MinimumSize.Height);
			}
			mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor &= ~AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor |= AnchorStyles.Top;
			buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
			buttonsTableLayoutPanel.Anchor |= AnchorStyles.Top;
			int num2 = Height - SizeFromClientSize(new Size(0, buttonsTableLayoutPanel.Top + buttonsTableLayoutPanel.Height + buttonsTableLayoutPanel.Margin.Bottom + Padding.Bottom)).Height;
			MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - num2);
			Height -= num2;
			buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Top;
			buttonsTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor &= ~AnchorStyles.Top;
			saveSelectionCheckBox.Anchor |= AnchorStyles.Bottom;
			mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
		}

		protected override void OnRightToLeftLayoutChanged(EventArgs e)
		{
			base.OnRightToLeftLayoutChanged(e);
			if (RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)dataSourceLabel, (Control)dataSourceListBox);
				LayoutUtils.MirrorControl((Control)dataProviderLabel, (Control)dataProviderComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)dataProviderLabel, (Control)dataProviderComboBox);
				LayoutUtils.UnmirrorControl((Control)dataSourceLabel, (Control)dataSourceListBox);
			}
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)dataSourceLabel, (Control)dataSourceListBox);
				LayoutUtils.MirrorControl((Control)dataProviderLabel, (Control)dataProviderComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)dataProviderLabel, (Control)dataProviderComboBox);
				LayoutUtils.UnmirrorControl((Control)dataSourceLabel, (Control)dataSourceListBox);
			}
		}

		protected override void OnHelpRequested(HelpEventArgs hevent)
		{
			Control activeControl = HelpUtils.GetActiveControl((Form)this);
			DataConnectionDialogContext context = DataConnectionDialogContext.Source;
			if (activeControl == dataSourceListBox)
				context = DataConnectionDialogContext.SourceListBox;
			if (activeControl == dataProviderComboBox)
				context = DataConnectionDialogContext.SourceProviderComboBox;
			if (activeControl == okButton)
				context = DataConnectionDialogContext.SourceOkButton;
			if (activeControl == cancelButton)
				context = DataConnectionDialogContext.SourceCancelButton;
			ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
			_mainDialog.OnContextHelpRequested(e);
			hevent.Handled = e.Handled;
			if (e.Handled)
				return;
			base.OnHelpRequested(hevent);
		}

		protected override void WndProc(ref Message m)
		{
			if (_mainDialog.TranslateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
				HelpUtils.TranslateContextHelpMessage((Form)this, ref m);
			base.WndProc(ref m);
		}

		private void FormatDataSource(object sender, ListControlConvertEventArgs e)
		{
			if (!(e.DesiredType == typeof(string)))
				return;
			e.Value = (object)(e.ListItem as DataSource).DisplayName;
		}

		private void ChangeDataSource(object sender, EventArgs e)
		{
			DataSource selectedItem = dataSourceListBox.SelectedItem as DataSource;
			dataProviderComboBox.Items.Clear();
			if (selectedItem != null)
			{
				foreach (object provider in (IEnumerable<DataProvider>)selectedItem.Providers)
					dataProviderComboBox.Items.Add(provider);
				if (!_providerSelections.ContainsKey(selectedItem))
					_providerSelections.Add(selectedItem, selectedItem.DefaultProvider);
				dataProviderComboBox.SelectedItem = (object)_providerSelections[selectedItem];
			}
			else
				dataProviderComboBox.Items.Add((object)string.Empty);
			ConfigureDescription();
			SetOkButtonStatus();
		}

		private void SelectDataSource(object sender, EventArgs e)
		{
			if (!okButton.Enabled)
				return;
			DialogResult = DialogResult.OK;
			DoOk(sender, e);
			Close();
		}

		private void FormatDataProvider(object sender, ListControlConvertEventArgs e)
		{
			if (!(e.DesiredType == typeof(string)))
				return;
			e.Value = e.ListItem is DataProvider ? (object)(e.ListItem as DataProvider).DisplayName : (object)e.ListItem.ToString();
		}

		private void SetDataProviderDropDownWidth(object sender, EventArgs e)
		{
			if (dataProviderComboBox.Items.Count > 0 && !(dataProviderComboBox.Items[0] is string))
			{
				int num = 0;
				using (Graphics dc = Graphics.FromHwnd(dataProviderComboBox.Handle))
				{
					foreach (DataProvider dataProvider in dataProviderComboBox.Items)
					{
						int width = TextRenderer.MeasureText((IDeviceContext)dc, dataProvider.DisplayName, dataProviderComboBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak).Width;
						if (width > num)
							num = width;
					}
				}
				dataProviderComboBox.DropDownWidth = num + 3;
				if (dataProviderComboBox.Items.Count <= dataProviderComboBox.MaxDropDownItems)
					return;
				dataProviderComboBox.DropDownWidth += SystemInformation.VerticalScrollBarWidth;
			}
			else
				dataProviderComboBox.DropDownWidth = dataProviderComboBox.Width;
		}

		private void ChangeDataProvider(object sender, EventArgs e)
		{
			if (dataSourceListBox.SelectedItem != null)
				_providerSelections[dataSourceListBox.SelectedItem as DataSource] = dataProviderComboBox.SelectedItem as DataProvider;
			ConfigureDescription();
			SetOkButtonStatus();
		}

		private void ConfigureDescription()
		{
			if (dataProviderComboBox.SelectedItem is DataProvider)
			{
				if (dataSourceListBox.SelectedItem == _mainDialog.UnspecifiedDataSource)
					descriptionLabel.Text = (dataProviderComboBox.SelectedItem as DataProvider).Description;
				else
					descriptionLabel.Text = (dataProviderComboBox.SelectedItem as DataProvider).GetDescription(dataSourceListBox.SelectedItem as DataSource);
			}
			else
				descriptionLabel.Text = (string)null;
		}

		private void SetSaveSelection(object sender, EventArgs e)
		{
			_mainDialog.SaveSelection = saveSelectionCheckBox.Checked;
		}

		private void SetOkButtonStatus()
		{
			okButton.Enabled = dataSourceListBox.SelectedItem is DataSource && dataProviderComboBox.SelectedItem is DataProvider;
		}

		private void DoOk(object sender, EventArgs e)
		{
			_mainDialog.SetSelectedDataSourceInternal(dataSourceListBox.SelectedItem as DataSource);
			foreach (DataSource dataSource in dataSourceListBox.Items)
			{
				DataProvider providerSelection = _providerSelections.ContainsKey(dataSource) ? _providerSelections[dataSource] : (DataProvider)null;
				_mainDialog.SetSelectedDataProviderInternal(dataSource, providerSelection);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(DataConnectionSourceDialog));
			mainTableLayoutPanel = new TableLayoutPanel();
			leftPanel = new Panel();
			dataSourceLabel = new Label();
			dataSourceListBox = new ListBox();
			dataProviderLabel = new Label();
			dataProviderComboBox = new ComboBox();
			descriptionGroupBox = new GroupBox();
			descriptionLabel = new Label();
			saveSelectionCheckBox = new CheckBox();
			buttonsTableLayoutPanel = new TableLayoutPanel();
			okButton = new Button();
			cancelButton = new Button();
			mainTableLayoutPanel.SuspendLayout();
			leftPanel.SuspendLayout();
			descriptionGroupBox.SuspendLayout();
			buttonsTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)mainTableLayoutPanel, "mainTableLayoutPanel");
			mainTableLayoutPanel.Controls.Add((Control)leftPanel, 0, 0);
			mainTableLayoutPanel.Controls.Add((Control)descriptionGroupBox, 1, 0);
			mainTableLayoutPanel.Name = "mainTableLayoutPanel";
			leftPanel.Controls.Add((Control)dataSourceLabel);
			leftPanel.Controls.Add((Control)dataSourceListBox);
			leftPanel.Controls.Add((Control)dataProviderLabel);
			leftPanel.Controls.Add((Control)dataProviderComboBox);
			componentResourceManager.ApplyResources((object)leftPanel, "leftPanel");
			leftPanel.Name = "leftPanel";
			componentResourceManager.ApplyResources((object)dataSourceLabel, "dataSourceLabel");
			dataSourceLabel.FlatStyle = FlatStyle.System;
			dataSourceLabel.Name = "dataSourceLabel";
			componentResourceManager.ApplyResources((object)dataSourceListBox, "dataSourceListBox");
			dataSourceListBox.FormattingEnabled = true;
			dataSourceListBox.MinimumSize = new Size(200, 108);
			dataSourceListBox.Name = "dataSourceListBox";
			dataSourceListBox.Sorted = true;
			dataSourceListBox.DoubleClick += new EventHandler(SelectDataSource);
			dataSourceListBox.SelectedIndexChanged += new EventHandler(ChangeDataSource);
			dataSourceListBox.Format += new ListControlConvertEventHandler(FormatDataSource);
			componentResourceManager.ApplyResources((object)dataProviderLabel, "dataProviderLabel");
			dataProviderLabel.FlatStyle = FlatStyle.System;
			dataProviderLabel.Name = "dataProviderLabel";
			componentResourceManager.ApplyResources((object)dataProviderComboBox, "dataProviderComboBox");
			dataProviderComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			dataProviderComboBox.FormattingEnabled = true;
			dataProviderComboBox.Items.AddRange(new object[1]
			{
		(object) componentResourceManager.GetString("dataProviderComboBox.Items")
			});
			dataProviderComboBox.Name = "dataProviderComboBox";
			dataProviderComboBox.Sorted = true;
			dataProviderComboBox.SelectedIndexChanged += new EventHandler(ChangeDataProvider);
			dataProviderComboBox.DropDown += new EventHandler(SetDataProviderDropDownWidth);
			dataProviderComboBox.Format += new ListControlConvertEventHandler(FormatDataProvider);
			componentResourceManager.ApplyResources((object)descriptionGroupBox, "descriptionGroupBox");
			descriptionGroupBox.Controls.Add((Control)descriptionLabel);
			descriptionGroupBox.FlatStyle = FlatStyle.System;
			descriptionGroupBox.Name = "descriptionGroupBox";
			descriptionGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)descriptionLabel, "descriptionLabel");
			descriptionLabel.FlatStyle = FlatStyle.System;
			descriptionLabel.Name = "descriptionLabel";
			componentResourceManager.ApplyResources((object)saveSelectionCheckBox, "saveSelectionCheckBox");
			saveSelectionCheckBox.Name = "saveSelectionCheckBox";
			saveSelectionCheckBox.CheckedChanged += new EventHandler(SetSaveSelection);
			componentResourceManager.ApplyResources((object)buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
			buttonsTableLayoutPanel.Controls.Add((Control)okButton, 0, 0);
			buttonsTableLayoutPanel.Controls.Add((Control)cancelButton, 1, 0);
			buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
			componentResourceManager.ApplyResources((object)okButton, "okButton");
			okButton.DialogResult = DialogResult.OK;
			okButton.MinimumSize = new Size(75, 23);
			okButton.Name = "okButton";
			okButton.Click += new EventHandler(DoOk);
			componentResourceManager.ApplyResources((object)cancelButton, "cancelButton");
			cancelButton.DialogResult = DialogResult.Cancel;
			cancelButton.MinimumSize = new Size(75, 23);
			cancelButton.Name = "cancelButton";
			AcceptButton = (IButtonControl)okButton;
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			CancelButton = (IButtonControl)cancelButton;
			Controls.Add((Control)mainTableLayoutPanel);
			Controls.Add((Control)saveSelectionCheckBox);
			Controls.Add((Control)buttonsTableLayoutPanel);
			HelpButton = true;
			MaximizeBox = false;
			MinimizeBox = false;
			Name = nameof(DataConnectionSourceDialog);
			ShowIcon = false;
			ShowInTaskbar = false;
			mainTableLayoutPanel.ResumeLayout(false);
			leftPanel.ResumeLayout(false);
			leftPanel.PerformLayout();
			descriptionGroupBox.ResumeLayout(false);
			buttonsTableLayoutPanel.ResumeLayout(false);
			buttonsTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}
	}
}
