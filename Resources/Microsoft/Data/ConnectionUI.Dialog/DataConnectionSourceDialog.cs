using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
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
      this.InitializeComponent();
      if (this.components == null)
        this.components = (IContainer) new System.ComponentModel.Container();
      this.components.Add((IComponent) new UserPreferenceChangedHandler((Form) this));
    }

    public DataConnectionSourceDialog(DataConnectionDialog mainDialog)
      : this()
    {
      this._mainDialog = mainDialog;
    }

    public string Title
    {
      get => this.Text;
      set => this.Text = value;
    }

    public string HeaderLabel
    {
      get => this._headerLabel == null ? string.Empty : this._headerLabel.Text;
      set
      {
        if (this._headerLabel == null)
        {
          switch (value)
          {
            case null:
              return;
            case "":
              return;
          }
        }
        if (this._headerLabel != null && value == this._headerLabel.Text)
          return;
        if (value != null)
        {
          if (this._headerLabel == null)
          {
            this._headerLabel = new Label();
            this._headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this._headerLabel.FlatStyle = FlatStyle.System;
            this._headerLabel.Location = new Point(12, 12);
            this._headerLabel.Margin = new Padding(3);
            this._headerLabel.Name = "dataSourceLabel";
            this._headerLabel.Width = this.mainTableLayoutPanel.Width;
            this._headerLabel.TabIndex = 100;
            this.Controls.Add((Control) this._headerLabel);
          }
          this._headerLabel.Text = value;
          this.MinimumSize = Size.Empty;
          this._headerLabel.Height = LayoutUtils.GetPreferredLabelHeight(this._headerLabel);
          int bottom1 = this._headerLabel.Bottom;
          Padding margin = this._headerLabel.Margin;
          int bottom2 = margin.Bottom;
          int num1 = bottom1 + bottom2;
          margin = this.mainTableLayoutPanel.Margin;
          int top = margin.Top;
          int num2 = num1 + top - this.mainTableLayoutPanel.Top;
          this.mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
          this.Height += num2;
          this.mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
          this.mainTableLayoutPanel.Top += num2;
          this.MinimumSize = this.Size;
        }
        else
        {
          if (this._headerLabel == null)
            return;
          int height = this._headerLabel.Height;
          try
          {
            this.Controls.Remove((Control) this._headerLabel);
          }
          finally
          {
            this._headerLabel.Dispose();
            this._headerLabel = (Label) null;
          }
          this.MinimumSize = Size.Empty;
          this.mainTableLayoutPanel.Top -= height;
          this.mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
          this.Height -= height;
          this.mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
          this.MinimumSize = this.Size;
        }
      }
    }

    protected override void OnLoad(EventArgs e)
    {
      if (this._mainDialog != null)
      {
        foreach (DataSource dataSource in (IEnumerable<DataSource>) this._mainDialog.DataSources)
        {
          if (dataSource != this._mainDialog.UnspecifiedDataSource)
            this.dataSourceListBox.Items.Add((object) dataSource);
        }
        if (this._mainDialog.DataSources.Contains(this._mainDialog.UnspecifiedDataSource))
        {
          this.dataSourceListBox.Sorted = false;
          this.dataSourceListBox.Items.Add((object) this._mainDialog.UnspecifiedDataSource);
        }
        int val1_1 = this.dataSourceListBox.Width - (this.dataSourceListBox.Width - this.dataSourceListBox.ClientSize.Width);
        foreach (object obj in this.dataSourceListBox.Items)
        {
          Size size = TextRenderer.MeasureText((obj as DataSource).DisplayName, this.dataSourceListBox.Font);
          size.Width += 3;
          val1_1 = Math.Max(val1_1, size.Width);
        }
        int num1 = val1_1;
        int width1 = this.dataSourceListBox.Width;
        Size size1 = this.dataSourceListBox.ClientSize;
        int width2 = size1.Width;
        int num2 = width1 - width2;
        int val1_2 = num1 + num2;
        size1 = this.dataSourceListBox.MinimumSize;
        int width3 = size1.Width;
        int num3 = Math.Max(val1_2, width3);
        size1 = this.dataSourceListBox.Size;
        int width4 = size1.Width;
        this.Width += (num3 - width4) * 2;
        this.MinimumSize = this.Size;
        if (this._mainDialog.SelectedDataSource != null)
        {
          this.dataSourceListBox.SelectedItem = (object) this._mainDialog.SelectedDataSource;
          if (this._mainDialog.SelectedDataProvider != null)
            this.dataProviderComboBox.SelectedItem = (object) this._mainDialog.SelectedDataProvider;
        }
        foreach (DataSource dataSource in this.dataSourceListBox.Items)
        {
          DataProvider selectedDataProvider = this._mainDialog.GetSelectedDataProvider(dataSource);
          if (selectedDataProvider != null)
            this._providerSelections[dataSource] = selectedDataProvider;
        }
      }
      this.saveSelectionCheckBox.Checked = this._mainDialog.SaveSelection;
      this.SetOkButtonStatus();
      base.OnLoad(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
      base.OnFontChanged(e);
      this.dataProviderComboBox.Top = this.leftPanel.Height - this.leftPanel.Padding.Bottom - this.dataProviderComboBox.Margin.Bottom - this.dataProviderComboBox.Height;
      this.dataProviderLabel.Top = this.dataProviderComboBox.Top - this.dataProviderComboBox.Margin.Top - this.dataProviderLabel.Margin.Bottom - this.dataProviderLabel.Height;
      int num1 = this.saveSelectionCheckBox.Right + this.saveSelectionCheckBox.Margin.Right - (this.buttonsTableLayoutPanel.Left - this.buttonsTableLayoutPanel.Margin.Left);
      if (num1 > 0)
      {
        this.Width += num1;
        this.MinimumSize = new Size(this.MinimumSize.Width + num1, this.MinimumSize.Height);
      }
      this.mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
      this.saveSelectionCheckBox.Anchor &= ~AnchorStyles.Bottom;
      this.saveSelectionCheckBox.Anchor |= AnchorStyles.Top;
      this.buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
      this.buttonsTableLayoutPanel.Anchor |= AnchorStyles.Top;
      int num2 = this.Height - this.SizeFromClientSize(new Size(0, this.buttonsTableLayoutPanel.Top + this.buttonsTableLayoutPanel.Height + this.buttonsTableLayoutPanel.Margin.Bottom + this.Padding.Bottom)).Height;
      Size minimumSize = this.MinimumSize;
      int width = minimumSize.Width;
      minimumSize = this.MinimumSize;
      int height = minimumSize.Height - num2;
      this.MinimumSize = new Size(width, height);
      this.Height -= num2;
      this.buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Top;
      this.buttonsTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
      this.saveSelectionCheckBox.Anchor &= ~AnchorStyles.Top;
      this.saveSelectionCheckBox.Anchor |= AnchorStyles.Bottom;
      this.mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
    }

    protected override void OnRightToLeftLayoutChanged(EventArgs e)
    {
      base.OnRightToLeftLayoutChanged(e);
      if (this.RightToLeftLayout && this.RightToLeft == RightToLeft.Yes)
      {
        LayoutUtils.MirrorControl((Control) this.dataSourceLabel, (Control) this.dataSourceListBox);
        LayoutUtils.MirrorControl((Control) this.dataProviderLabel, (Control) this.dataProviderComboBox);
      }
      else
      {
        LayoutUtils.UnmirrorControl((Control) this.dataProviderLabel, (Control) this.dataProviderComboBox);
        LayoutUtils.UnmirrorControl((Control) this.dataSourceLabel, (Control) this.dataSourceListBox);
      }
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
      base.OnRightToLeftChanged(e);
      if (this.RightToLeftLayout && this.RightToLeft == RightToLeft.Yes)
      {
        LayoutUtils.MirrorControl((Control) this.dataSourceLabel, (Control) this.dataSourceListBox);
        LayoutUtils.MirrorControl((Control) this.dataProviderLabel, (Control) this.dataProviderComboBox);
      }
      else
      {
        LayoutUtils.UnmirrorControl((Control) this.dataProviderLabel, (Control) this.dataProviderComboBox);
        LayoutUtils.UnmirrorControl((Control) this.dataSourceLabel, (Control) this.dataSourceListBox);
      }
    }

    protected override void OnHelpRequested(HelpEventArgs hevent)
    {
      Control activeControl = HelpUtils.GetActiveControl((Form) this);
      DataConnectionDialogContext context = DataConnectionDialogContext.Source;
      if (activeControl == this.dataSourceListBox)
        context = DataConnectionDialogContext.SourceListBox;
      if (activeControl == this.dataProviderComboBox)
        context = DataConnectionDialogContext.SourceProviderComboBox;
      if (activeControl == this.okButton)
        context = DataConnectionDialogContext.SourceOkButton;
      if (activeControl == this.cancelButton)
        context = DataConnectionDialogContext.SourceCancelButton;
      ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
      this._mainDialog.OnContextHelpRequested(e);
      hevent.Handled = e.Handled;
      if (e.Handled)
        return;
      base.OnHelpRequested(hevent);
    }

    protected override void WndProc(ref Message m)
    {
      if (this._mainDialog.TranslateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
        HelpUtils.TranslateContextHelpMessage((Form) this, ref m);
      base.WndProc(ref m);
    }

    private void FormatDataSource(object sender, ListControlConvertEventArgs e)
    {
      if (!(e.DesiredType == typeof (string)))
        return;
      e.Value = (object) (e.ListItem as DataSource).DisplayName;
    }

    private void ChangeDataSource(object sender, EventArgs e)
    {
      DataSource selectedItem = this.dataSourceListBox.SelectedItem as DataSource;
      this.dataProviderComboBox.Items.Clear();
      if (selectedItem != null)
      {
        foreach (object provider in (IEnumerable<DataProvider>) selectedItem.Providers)
          this.dataProviderComboBox.Items.Add(provider);
        if (!this._providerSelections.ContainsKey(selectedItem))
          this._providerSelections.Add(selectedItem, selectedItem.DefaultProvider);
        this.dataProviderComboBox.SelectedItem = (object) this._providerSelections[selectedItem];
      }
      else
        this.dataProviderComboBox.Items.Add((object) string.Empty);
      this.ConfigureDescription();
      this.SetOkButtonStatus();
    }

    private void SelectDataSource(object sender, EventArgs e)
    {
      if (!this.okButton.Enabled)
        return;
      this.DialogResult = DialogResult.OK;
      this.DoOk(sender, e);
      this.Close();
    }

    private void FormatDataProvider(object sender, ListControlConvertEventArgs e)
    {
      if (!(e.DesiredType == typeof (string)))
        return;
      e.Value = e.ListItem is DataProvider ? (object) (e.ListItem as DataProvider).DisplayName : (object) e.ListItem.ToString();
    }

    private void SetDataProviderDropDownWidth(object sender, EventArgs e)
    {
      if (this.dataProviderComboBox.Items.Count > 0 && !(this.dataProviderComboBox.Items[0] is string))
      {
        int num = 0;
        using (Graphics dc = Graphics.FromHwnd(this.dataProviderComboBox.Handle))
        {
          foreach (DataProvider dataProvider in this.dataProviderComboBox.Items)
          {
            int width = TextRenderer.MeasureText((IDeviceContext) dc, dataProvider.DisplayName, this.dataProviderComboBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak).Width;
            if (width > num)
              num = width;
          }
        }
        this.dataProviderComboBox.DropDownWidth = num + 3;
        if (this.dataProviderComboBox.Items.Count <= this.dataProviderComboBox.MaxDropDownItems)
          return;
        this.dataProviderComboBox.DropDownWidth += SystemInformation.VerticalScrollBarWidth;
      }
      else
        this.dataProviderComboBox.DropDownWidth = Math.Max(1, this.dataProviderComboBox.Width);
    }

    private void ChangeDataProvider(object sender, EventArgs e)
    {
      if (this.dataSourceListBox.SelectedItem != null)
        this._providerSelections[this.dataSourceListBox.SelectedItem as DataSource] = this.dataProviderComboBox.SelectedItem as DataProvider;
      this.ConfigureDescription();
      this.SetOkButtonStatus();
    }

    private void ConfigureDescription()
    {
      if (this.dataProviderComboBox.SelectedItem is DataProvider)
      {
        if (this.dataSourceListBox.SelectedItem == this._mainDialog.UnspecifiedDataSource)
          this.descriptionLabel.Text = (this.dataProviderComboBox.SelectedItem as DataProvider).Description;
        else
          this.descriptionLabel.Text = (this.dataProviderComboBox.SelectedItem as DataProvider).GetDescription(this.dataSourceListBox.SelectedItem as DataSource);
      }
      else
        this.descriptionLabel.Text = (string) null;
    }

    private void SetSaveSelection(object sender, EventArgs e)
    {
      this._mainDialog.SaveSelection = this.saveSelectionCheckBox.Checked;
    }

    private void SetOkButtonStatus()
    {
      this.okButton.Enabled = this.dataSourceListBox.SelectedItem is DataSource && this.dataProviderComboBox.SelectedItem is DataProvider;
    }

    private void DoOk(object sender, EventArgs e)
    {
      this._mainDialog.SetSelectedDataSourceInternal(this.dataSourceListBox.SelectedItem as DataSource);
      foreach (DataSource dataSource in this.dataSourceListBox.Items)
      {
        DataProvider providerSelection = this._providerSelections.ContainsKey(dataSource) ? this._providerSelections[dataSource] : (DataProvider) null;
        this._mainDialog.SetSelectedDataProviderInternal(dataSource, providerSelection);
      }
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (DataConnectionSourceDialog));
      this.mainTableLayoutPanel = new TableLayoutPanel();
      this.leftPanel = new Panel();
      this.dataSourceLabel = new Label();
      this.dataSourceListBox = new ListBox();
      this.dataProviderLabel = new Label();
      this.dataProviderComboBox = new ComboBox();
      this.descriptionGroupBox = new GroupBox();
      this.descriptionLabel = new Label();
      this.saveSelectionCheckBox = new CheckBox();
      this.buttonsTableLayoutPanel = new TableLayoutPanel();
      this.okButton = new Button();
      this.cancelButton = new Button();
      this.mainTableLayoutPanel.SuspendLayout();
      this.leftPanel.SuspendLayout();
      this.descriptionGroupBox.SuspendLayout();
      this.buttonsTableLayoutPanel.SuspendLayout();
      this.SuspendLayout();
      componentResourceManager.ApplyResources((object) this.mainTableLayoutPanel, "mainTableLayoutPanel");
      this.mainTableLayoutPanel.Controls.Add((Control) this.leftPanel, 0, 0);
      this.mainTableLayoutPanel.Controls.Add((Control) this.descriptionGroupBox, 1, 0);
      this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
      this.leftPanel.Controls.Add((Control) this.dataSourceLabel);
      this.leftPanel.Controls.Add((Control) this.dataSourceListBox);
      this.leftPanel.Controls.Add((Control) this.dataProviderLabel);
      this.leftPanel.Controls.Add((Control) this.dataProviderComboBox);
      componentResourceManager.ApplyResources((object) this.leftPanel, "leftPanel");
      this.leftPanel.Name = "leftPanel";
      componentResourceManager.ApplyResources((object) this.dataSourceLabel, "dataSourceLabel");
      this.dataSourceLabel.FlatStyle = FlatStyle.System;
      this.dataSourceLabel.Name = "dataSourceLabel";
      componentResourceManager.ApplyResources((object) this.dataSourceListBox, "dataSourceListBox");
      this.dataSourceListBox.FormattingEnabled = true;
      this.dataSourceListBox.MinimumSize = new Size(200, 108);
      this.dataSourceListBox.Name = "dataSourceListBox";
      this.dataSourceListBox.Sorted = true;
      this.dataSourceListBox.DoubleClick += new EventHandler(this.SelectDataSource);
      this.dataSourceListBox.SelectedIndexChanged += new EventHandler(this.ChangeDataSource);
      this.dataSourceListBox.Format += new ListControlConvertEventHandler(this.FormatDataSource);
      componentResourceManager.ApplyResources((object) this.dataProviderLabel, "dataProviderLabel");
      this.dataProviderLabel.FlatStyle = FlatStyle.System;
      this.dataProviderLabel.Name = "dataProviderLabel";
      componentResourceManager.ApplyResources((object) this.dataProviderComboBox, "dataProviderComboBox");
      this.dataProviderComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
      this.dataProviderComboBox.FormattingEnabled = true;
      this.dataProviderComboBox.Items.AddRange(new object[1]
      {
        (object) componentResourceManager.GetString("dataProviderComboBox.Items")
      });
      this.dataProviderComboBox.Name = "dataProviderComboBox";
      this.dataProviderComboBox.Sorted = true;
      this.dataProviderComboBox.SelectedIndexChanged += new EventHandler(this.ChangeDataProvider);
      this.dataProviderComboBox.DropDown += new EventHandler(this.SetDataProviderDropDownWidth);
      this.dataProviderComboBox.Format += new ListControlConvertEventHandler(this.FormatDataProvider);
      componentResourceManager.ApplyResources((object) this.descriptionGroupBox, "descriptionGroupBox");
      this.descriptionGroupBox.Controls.Add((Control) this.descriptionLabel);
      this.descriptionGroupBox.FlatStyle = FlatStyle.System;
      this.descriptionGroupBox.Name = "descriptionGroupBox";
      this.descriptionGroupBox.TabStop = false;
      componentResourceManager.ApplyResources((object) this.descriptionLabel, "descriptionLabel");
      this.descriptionLabel.FlatStyle = FlatStyle.System;
      this.descriptionLabel.Name = "descriptionLabel";
      componentResourceManager.ApplyResources((object) this.saveSelectionCheckBox, "saveSelectionCheckBox");
      this.saveSelectionCheckBox.Name = "saveSelectionCheckBox";
      this.saveSelectionCheckBox.CheckedChanged += new EventHandler(this.SetSaveSelection);
      componentResourceManager.ApplyResources((object) this.buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
      this.buttonsTableLayoutPanel.Controls.Add((Control) this.okButton, 0, 0);
      this.buttonsTableLayoutPanel.Controls.Add((Control) this.cancelButton, 1, 0);
      this.buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.okButton, "okButton");
      this.okButton.DialogResult = DialogResult.OK;
      this.okButton.MinimumSize = new Size(75, 23);
      this.okButton.Name = "okButton";
      this.okButton.Click += new EventHandler(this.DoOk);
      componentResourceManager.ApplyResources((object) this.cancelButton, "cancelButton");
      this.cancelButton.DialogResult = DialogResult.Cancel;
      this.cancelButton.MinimumSize = new Size(75, 23);
      this.cancelButton.Name = "cancelButton";
      this.AcceptButton = (IButtonControl) this.okButton;
      componentResourceManager.ApplyResources((object) this, "$this");
      this.AutoScaleMode = AutoScaleMode.Font;
      this.CancelButton = (IButtonControl) this.cancelButton;
      this.Controls.Add((Control) this.mainTableLayoutPanel);
      this.Controls.Add((Control) this.saveSelectionCheckBox);
      this.Controls.Add((Control) this.buttonsTableLayoutPanel);
      this.HelpButton = true;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = nameof (DataConnectionSourceDialog);
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.mainTableLayoutPanel.ResumeLayout(false);
      this.leftPanel.ResumeLayout(false);
      this.leftPanel.PerformLayout();
      this.descriptionGroupBox.ResumeLayout(false);
      this.buttonsTableLayoutPanel.ResumeLayout(false);
      this.buttonsTableLayoutPanel.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
