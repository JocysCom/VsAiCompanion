using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.ComponentModel.Com2Interop;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal class DataConnectionAdvancedDialog : Form
  {
    private string _savedConnectionString;
    private DataConnectionDialog _mainDialog;
    private IContainer components;
    private TextBox textBox;
    private TableLayoutPanel buttonsTableLayoutPanel;
    private Button okButton;
    private Button cancelButton;
    private DataConnectionAdvancedDialog.SpecializedPropertyGrid propertyGrid;

    public DataConnectionAdvancedDialog()
    {
      this.InitializeComponent();
      if (this.components == null)
        this.components = (IContainer) new System.ComponentModel.Container();
      this.components.Add((IComponent) new UserPreferenceChangedHandler((Form) this));
    }

    public DataConnectionAdvancedDialog(
      IDataConnectionProperties connectionProperties,
      DataConnectionDialog mainDialog)
      : this()
    {
      this._savedConnectionString = connectionProperties.ToFullString();
      this.propertyGrid.SelectedObject = (object) connectionProperties;
      this._mainDialog = mainDialog;
    }

    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);
      this.ConfigureTextBox();
    }

    protected override void OnShown(EventArgs e)
    {
      base.OnShown(e);
      this.propertyGrid.Focus();
    }

    protected override void OnFontChanged(EventArgs e)
    {
      base.OnFontChanged(e);
      this.textBox.Width = this.propertyGrid.Width;
    }

    protected override void OnHelpRequested(HelpEventArgs hevent)
    {
      Control control = (Control) this;
      while (control is ContainerControl containerControl && containerControl != this.propertyGrid && containerControl.ActiveControl != null)
        control = containerControl.ActiveControl;
      DataConnectionDialogContext context = DataConnectionDialogContext.Advanced;
      if (control == this.propertyGrid)
        context = DataConnectionDialogContext.AdvancedPropertyGrid;
      if (control == this.textBox)
        context = DataConnectionDialogContext.AdvancedTextBox;
      if (control == this.okButton)
        context = DataConnectionDialogContext.AdvancedOkButton;
      if (control == this.cancelButton)
        context = DataConnectionDialogContext.AdvancedCancelButton;
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

    private void SetTextBox(object s, PropertyValueChangedEventArgs e) => this.ConfigureTextBox();

    private void ConfigureTextBox()
    {
      if (this.propertyGrid.SelectedObject is IDataConnectionProperties)
      {
        try
        {
          this.textBox.Text = (this.propertyGrid.SelectedObject as IDataConnectionProperties).ToDisplayString();
        }
        catch
        {
          this.textBox.Text = (string) null;
        }
      }
      else
        this.textBox.Text = (string) null;
    }

    private void RevertProperties(object sender, EventArgs e)
    {
      try
      {
        (this.propertyGrid.SelectedObject as IDataConnectionProperties).Parse(this._savedConnectionString);
      }
      catch
      {
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
      this.components = (IContainer) new System.ComponentModel.Container();
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (DataConnectionAdvancedDialog));
      this.propertyGrid = new DataConnectionAdvancedDialog.SpecializedPropertyGrid();
      this.textBox = new TextBox();
      this.buttonsTableLayoutPanel = new TableLayoutPanel();
      this.okButton = new Button();
      this.cancelButton = new Button();
      this.buttonsTableLayoutPanel.SuspendLayout();
      this.SuspendLayout();
      componentResourceManager.ApplyResources((object) this.propertyGrid, "propertyGrid");
      this.propertyGrid.CommandsActiveLinkColor = SystemColors.ActiveCaption;
      this.propertyGrid.CommandsDisabledLinkColor = SystemColors.ControlDark;
      this.propertyGrid.CommandsLinkColor = SystemColors.ActiveCaption;
      this.propertyGrid.MinimumSize = new Size(270, 250);
      this.propertyGrid.Name = "propertyGrid";
      this.propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler(this.SetTextBox);
      componentResourceManager.ApplyResources((object) this.textBox, "textBox");
      this.textBox.Name = "textBox";
      this.textBox.ReadOnly = true;
      this.textBox.TabStop = false;
      componentResourceManager.ApplyResources((object) this.buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
      this.buttonsTableLayoutPanel.Controls.Add((Control) this.okButton, 0, 0);
      this.buttonsTableLayoutPanel.Controls.Add((Control) this.cancelButton, 1, 0);
      this.buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.okButton, "okButton");
      this.okButton.DialogResult = DialogResult.OK;
      this.okButton.MinimumSize = new Size(75, 23);
      this.okButton.Name = "okButton";
      componentResourceManager.ApplyResources((object) this.cancelButton, "cancelButton");
      this.cancelButton.DialogResult = DialogResult.Cancel;
      this.cancelButton.MinimumSize = new Size(75, 23);
      this.cancelButton.Name = "cancelButton";
      this.cancelButton.Click += new EventHandler(this.RevertProperties);
      this.AcceptButton = (IButtonControl) this.okButton;
      componentResourceManager.ApplyResources((object) this, "$this");
      this.AutoScaleMode = AutoScaleMode.Font;
      this.CancelButton = (IButtonControl) this.cancelButton;
      this.Controls.Add((Control) this.buttonsTableLayoutPanel);
      this.Controls.Add((Control) this.textBox);
      this.Controls.Add((Control) this.propertyGrid);
      this.HelpButton = true;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = nameof (DataConnectionAdvancedDialog);
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.buttonsTableLayoutPanel.ResumeLayout(false);
      this.buttonsTableLayoutPanel.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    internal class SpecializedPropertyGrid : PropertyGrid
    {
      private ContextMenuStrip _contextMenu;

      public SpecializedPropertyGrid()
      {
        this._contextMenu = new ContextMenuStrip();
        this._contextMenu.Items.AddRange(new ToolStripItem[6]
        {
          (ToolStripItem) new ToolStripMenuItem(),
          (ToolStripItem) new ToolStripSeparator(),
          (ToolStripItem) new ToolStripMenuItem(),
          (ToolStripItem) new ToolStripMenuItem(),
          (ToolStripItem) new ToolStripSeparator(),
          (ToolStripItem) new ToolStripMenuItem()
        });
        this._contextMenu.Items[0].Text = SR.DataConnectionAdvancedDialog_Reset;
        this._contextMenu.Items[0].Click += new EventHandler(this.ResetProperty);
        this._contextMenu.Items[2].Text = SR.DataConnectionAdvancedDialog_Add;
        this._contextMenu.Items[2].Click += new EventHandler(this.AddProperty);
        this._contextMenu.Items[3].Text = SR.DataConnectionAdvancedDialog_Remove;
        this._contextMenu.Items[3].Click += new EventHandler(this.RemoveProperty);
        this._contextMenu.Items[5].Text = SR.DataConnectionAdvancedDialog_Description;
        this._contextMenu.Items[5].Click += new EventHandler(this.ToggleDescription);
        (this._contextMenu.Items[5] as ToolStripMenuItem).Checked = this.HelpVisible;
        this._contextMenu.Opened += new EventHandler(this.SetupContextMenu);
        this.ContextMenuStrip = this._contextMenu;
        this.DrawFlatToolbar = true;
        this.Size = new Size(270, 250);
        this.MinimumSize = this.Size;
      }

      protected override void OnHandleCreated(EventArgs e)
      {
        ProfessionalColorTable service = this.ParentForm == null || this.ParentForm.Site == null ? (ProfessionalColorTable) null : this.ParentForm.Site.GetService(typeof (ProfessionalColorTable)) as ProfessionalColorTable;
        if (service != null)
          this.ToolStripRenderer = (ToolStripRenderer) new ToolStripProfessionalRenderer(service);
        base.OnHandleCreated(e);
      }

      protected override void OnFontChanged(EventArgs e)
      {
        base.OnFontChanged(e);
        this.LargeButtons = (double) this.Font.SizeInPoints >= 15.0;
      }

      protected override void WndProc(ref Message m)
      {
        if (m.Msg == 7)
        {
          this.Focus();
          ((IComPropertyBrowser) this).HandleF4();
        }
        base.WndProc(ref m);
      }

      private void SetupContextMenu(object sender, EventArgs e)
      {
        this._contextMenu.Items[0].Enabled = this.SelectedGridItem.GridItemType == GridItemType.Property;
        if (this._contextMenu.Items[0].Enabled && this.SelectedGridItem.PropertyDescriptor != null)
        {
          object component = this.SelectedObject;
          if (this.SelectedObject is ICustomTypeDescriptor)
            component = (this.SelectedObject as ICustomTypeDescriptor).GetPropertyOwner(this.SelectedGridItem.PropertyDescriptor);
          this._contextMenu.Items[0].Enabled = this._contextMenu.Items[3].Enabled = this.SelectedGridItem.PropertyDescriptor.CanResetValue(component);
        }
        this._contextMenu.Items[2].Visible = this._contextMenu.Items[3].Visible = (this.SelectedObject as IDataConnectionProperties).IsExtensible;
        if (this._contextMenu.Items[3].Visible)
        {
          this._contextMenu.Items[3].Enabled = this.SelectedGridItem.GridItemType == GridItemType.Property;
          if (this._contextMenu.Items[3].Enabled && this.SelectedGridItem.PropertyDescriptor != null)
            this._contextMenu.Items[3].Enabled = !this.SelectedGridItem.PropertyDescriptor.IsReadOnly;
        }
        this._contextMenu.Items[1].Visible = this._contextMenu.Items[2].Visible || this._contextMenu.Items[3].Visible;
      }

      private void ResetProperty(object sender, EventArgs e)
      {
        object oldValue = this.SelectedGridItem.Value;
        object component = this.SelectedObject;
        if (this.SelectedObject is ICustomTypeDescriptor)
          component = (this.SelectedObject as ICustomTypeDescriptor).GetPropertyOwner(this.SelectedGridItem.PropertyDescriptor);
        this.SelectedGridItem.PropertyDescriptor.ResetValue(component);
        this.Refresh();
        this.OnPropertyValueChanged(new PropertyValueChangedEventArgs(this.SelectedGridItem, oldValue));
      }

      private void AddProperty(object sender, EventArgs e)
      {
        if (!(this.ParentForm is DataConnectionDialog mainDialog))
          mainDialog = (this.ParentForm as DataConnectionAdvancedDialog)._mainDialog;
        AddPropertyDialog addPropertyDialog = new AddPropertyDialog(mainDialog);
        try
        {
          if (this.ParentForm.Container != null)
            this.ParentForm.Container.Add((IComponent) addPropertyDialog);
          if (addPropertyDialog.ShowDialog((IWin32Window) this.ParentForm) != DialogResult.OK)
            return;
          (this.SelectedObject as IDataConnectionProperties).Add(addPropertyDialog.PropertyName);
          this.Refresh();
          GridItem currentItem = this.SelectedGridItem;
          while (currentItem.Parent != null)
            currentItem = currentItem.Parent;
          GridItem gridItem = this.LocateGridItem(currentItem, addPropertyDialog.PropertyName);
          if (gridItem == null)
            return;
          this.SelectedGridItem = gridItem;
        }
        finally
        {
          if (this.ParentForm.Container != null)
            this.ParentForm.Container.Remove((IComponent) addPropertyDialog);
          addPropertyDialog.Dispose();
        }
      }

      private void RemoveProperty(object sender, EventArgs e)
      {
        (this.SelectedObject as IDataConnectionProperties).Remove(this.SelectedGridItem.Label);
        this.Refresh();
        this.OnPropertyValueChanged(new PropertyValueChangedEventArgs((GridItem) null, (object) null));
      }

      private void ToggleDescription(object sender, EventArgs e)
      {
        this.HelpVisible = !this.HelpVisible;
        (this._contextMenu.Items[5] as ToolStripMenuItem).Checked = !(this._contextMenu.Items[5] as ToolStripMenuItem).Checked;
      }

      private GridItem LocateGridItem(GridItem currentItem, string propertyName)
      {
        if (currentItem.GridItemType == GridItemType.Property && currentItem.Label.Equals(propertyName, StringComparison.CurrentCulture))
          return currentItem;
        GridItem gridItem1 = (GridItem) null;
        foreach (GridItem gridItem2 in currentItem.GridItems)
        {
          gridItem1 = this.LocateGridItem(gridItem2, propertyName);
          if (gridItem1 != null)
            break;
        }
        return gridItem1;
      }
    }
  }
}
