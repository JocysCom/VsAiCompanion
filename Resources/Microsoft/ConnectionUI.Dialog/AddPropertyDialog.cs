using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal class AddPropertyDialog : Form
  {
    private DataConnectionDialog _mainDialog;
    private IContainer components;
    private Label propertyLabel;
    private TextBox propertyTextBox;
    private TableLayoutPanel buttonsTableLayoutPanel;
    private Button okButton;
    private Button cancelButton;

    public AddPropertyDialog()
    {
      this.InitializeComponent();
      if (this.components == null)
        this.components = (IContainer) new System.ComponentModel.Container();
      this.components.Add((IComponent) new UserPreferenceChangedHandler((Form) this));
    }

    public AddPropertyDialog(DataConnectionDialog mainDialog)
      : this()
    {
      this._mainDialog = mainDialog;
    }

    public string PropertyName => this.propertyTextBox.Text;

    protected override void OnFontChanged(EventArgs e)
    {
      base.OnFontChanged(e);
      this.propertyTextBox.Width = this.buttonsTableLayoutPanel.Right - this.propertyTextBox.Left;
      Padding padding = this.Padding;
      int left1 = padding.Left;
      padding = this.buttonsTableLayoutPanel.Margin;
      int left2 = padding.Left;
      int num1 = left1 + left2 + this.buttonsTableLayoutPanel.Width;
      padding = this.buttonsTableLayoutPanel.Margin;
      int right1 = padding.Right;
      int num2 = num1 + right1;
      padding = this.Padding;
      int right2 = padding.Right;
      int width = num2 + right2;
      if (this.ClientSize.Width >= width)
        return;
      this.ClientSize = new Size(width, this.ClientSize.Height);
    }

    protected override void OnHelpRequested(HelpEventArgs hevent)
    {
      Control activeControl = HelpUtils.GetActiveControl((Form) this);
      DataConnectionDialogContext context = DataConnectionDialogContext.AddProperty;
      if (activeControl == this.propertyTextBox)
        context = DataConnectionDialogContext.AddPropertyTextBox;
      if (activeControl == this.okButton)
        context = DataConnectionDialogContext.AddPropertyOkButton;
      if (activeControl == this.cancelButton)
        context = DataConnectionDialogContext.AddPropertyCancelButton;
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

    private void SetOkButtonStatus(object sender, EventArgs e)
    {
      this.okButton.Enabled = this.propertyTextBox.Text.Trim().Length > 0;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (AddPropertyDialog));
      this.propertyLabel = new Label();
      this.propertyTextBox = new TextBox();
      this.buttonsTableLayoutPanel = new TableLayoutPanel();
      this.okButton = new Button();
      this.cancelButton = new Button();
      this.buttonsTableLayoutPanel.SuspendLayout();
      this.SuspendLayout();
      componentResourceManager.ApplyResources((object) this.propertyLabel, "propertyLabel");
      this.propertyLabel.FlatStyle = FlatStyle.System;
      this.propertyLabel.Name = "propertyLabel";
      componentResourceManager.ApplyResources((object) this.propertyTextBox, "propertyTextBox");
      this.propertyTextBox.Name = "propertyTextBox";
      this.propertyTextBox.TextChanged += new EventHandler(this.SetOkButtonStatus);
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
      this.AcceptButton = (IButtonControl) this.okButton;
      componentResourceManager.ApplyResources((object) this, "$this");
      this.AutoScaleMode = AutoScaleMode.Font;
      this.CancelButton = (IButtonControl) this.cancelButton;
      this.Controls.Add((Control) this.buttonsTableLayoutPanel);
      this.Controls.Add((Control) this.propertyTextBox);
      this.Controls.Add((Control) this.propertyLabel);
      this.FormBorderStyle = FormBorderStyle.FixedDialog;
      this.HelpButton = true;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = nameof (AddPropertyDialog);
      this.ShowInTaskbar = false;
      this.buttonsTableLayoutPanel.ResumeLayout(false);
      this.buttonsTableLayoutPanel.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
