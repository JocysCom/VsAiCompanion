using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
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
			InitializeComponent();
			if (components == null)
				components = (IContainer)new System.ComponentModel.Container();
			components.Add((IComponent)new UserPreferenceChangedHandler((Form)this));
		}

		public AddPropertyDialog(DataConnectionDialog mainDialog)
		  : this()
		{
			_mainDialog = mainDialog;
		}

		public string PropertyName => propertyTextBox.Text;

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			propertyTextBox.Width = buttonsTableLayoutPanel.Right - propertyTextBox.Left;
			int width = Padding.Left + buttonsTableLayoutPanel.Margin.Left + buttonsTableLayoutPanel.Width + buttonsTableLayoutPanel.Margin.Right + Padding.Right;
			if (ClientSize.Width >= width)
				return;
			ClientSize = new Size(width, ClientSize.Height);
		}

		protected override void OnHelpRequested(HelpEventArgs hevent)
		{
			Control activeControl = HelpUtils.GetActiveControl((Form)this);
			DataConnectionDialogContext context = DataConnectionDialogContext.AddProperty;
			if (activeControl == propertyTextBox)
				context = DataConnectionDialogContext.AddPropertyTextBox;
			if (activeControl == okButton)
				context = DataConnectionDialogContext.AddPropertyOkButton;
			if (activeControl == cancelButton)
				context = DataConnectionDialogContext.AddPropertyCancelButton;
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

		private void SetOkButtonStatus(object sender, EventArgs e)
		{
			okButton.Enabled = propertyTextBox.Text.Trim().Length > 0;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(AddPropertyDialog));
			propertyLabel = new Label();
			propertyTextBox = new TextBox();
			buttonsTableLayoutPanel = new TableLayoutPanel();
			okButton = new Button();
			cancelButton = new Button();
			buttonsTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)propertyLabel, "propertyLabel");
			propertyLabel.FlatStyle = FlatStyle.System;
			propertyLabel.Name = "propertyLabel";
			componentResourceManager.ApplyResources((object)propertyTextBox, "propertyTextBox");
			propertyTextBox.Name = "propertyTextBox";
			propertyTextBox.TextChanged += new EventHandler(SetOkButtonStatus);
			componentResourceManager.ApplyResources((object)buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
			buttonsTableLayoutPanel.Controls.Add((Control)okButton, 0, 0);
			buttonsTableLayoutPanel.Controls.Add((Control)cancelButton, 1, 0);
			buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
			componentResourceManager.ApplyResources((object)okButton, "okButton");
			okButton.DialogResult = DialogResult.OK;
			okButton.MinimumSize = new Size(75, 23);
			okButton.Name = "okButton";
			componentResourceManager.ApplyResources((object)cancelButton, "cancelButton");
			cancelButton.DialogResult = DialogResult.Cancel;
			cancelButton.MinimumSize = new Size(75, 23);
			cancelButton.Name = "cancelButton";
			AcceptButton = (IButtonControl)okButton;
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			CancelButton = (IButtonControl)cancelButton;
			Controls.Add((Control)buttonsTableLayoutPanel);
			Controls.Add((Control)propertyTextBox);
			Controls.Add((Control)propertyLabel);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			HelpButton = true;
			MaximizeBox = false;
			MinimizeBox = false;
			Name = nameof(AddPropertyDialog);
			ShowInTaskbar = false;
			buttonsTableLayoutPanel.ResumeLayout(false);
			buttonsTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}
	}
}
