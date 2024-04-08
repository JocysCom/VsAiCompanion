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
	public class OracleConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private IDataConnectionProperties _connectionProperties;
		private IContainer components;
		private Label serverLabel;
		private TextBox serverTextBox;
		private GroupBox logonGroupBox;
		private TableLayoutPanel loginTableLayoutPanel;
		private Label userNameLabel;
		private TextBox userNameTextBox;
		private Label passwordLabel;
		private TextBox passwordTextBox;
		private CheckBox savePasswordCheckBox;

		public OracleConnectionUIControl()
		{
			InitializeComponent();
			RightToLeft = RightToLeft.Inherit;
			if (savePasswordCheckBox.Height >= LayoutUtils.GetPreferredCheckBoxHeight(savePasswordCheckBox))
				return;
			savePasswordCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			loginTableLayoutPanel.Height += loginTableLayoutPanel.Margin.Bottom;
			loginTableLayoutPanel.Margin = new Padding(loginTableLayoutPanel.Margin.Left, loginTableLayoutPanel.Margin.Top, loginTableLayoutPanel.Margin.Right, 0);
		}

		public void Initialize(IDataConnectionProperties connectionProperties)
		{
			switch (connectionProperties)
			{
				case null:
					throw new ArgumentNullException(nameof(connectionProperties));
				case OracleConnectionProperties _:
				case OleDBOracleConnectionProperties _:
					if (connectionProperties is OdbcConnectionProperties)
						savePasswordCheckBox.Enabled = false;
					_connectionProperties = connectionProperties;
					break;
				default:
					throw new ArgumentException(SR.GetString("OracleConnectionUIControl_InvalidConnectionProperties"));
			}
		}

		public void LoadProperties()
		{
			_loading = true;
			serverTextBox.Text = Properties[ServerProperty] as string;
			userNameTextBox.Text = Properties[UserNameProperty] as string;
			passwordTextBox.Text = Properties[PasswordProperty] as string;
			savePasswordCheckBox.Checked = !(Properties is OdbcConnectionProperties) && (bool)Properties["Persist Security Info"];
			_loading = false;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
				LayoutUtils.MirrorControl((Control)serverLabel, (Control)serverTextBox);
			else
				LayoutUtils.UnmirrorControl((Control)serverLabel, (Control)serverTextBox);
		}

		protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
		{
			Size size = Size;
			MinimumSize = Size.Empty;
			base.ScaleControl(factor, specified);
			MinimumSize = new Size((int)Math.Round((double)size.Width * (double)factor.Width), (int)Math.Round((double)size.Height * (double)factor.Height));
		}

		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			if (Parent != null)
				return;
			OnFontChanged(e);
		}

		private void SetServer(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties[ServerProperty] = serverTextBox.Text.Trim().Length > 0 ? (object)serverTextBox.Text.Trim() : (object)(string)null;
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties[UserNameProperty] = userNameTextBox.Text.Trim().Length > 0 ? (object)userNameTextBox.Text.Trim() : (object)(string)null;
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties[PasswordProperty] = passwordTextBox.Text.Length > 0 ? (object)passwordTextBox.Text : (object)(string)null;
			passwordTextBox.Text = passwordTextBox.Text;
		}

		private void SetSavePassword(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["Persist Security Info"] = (object)savePasswordCheckBox.Checked;
		}

		private void TrimControlText(object sender, EventArgs e)
		{
			Control control = sender as Control;
			control.Text = control.Text.Trim();
		}

		private string ServerProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "Data Source" : "SERVER";
		}

		private string UserNameProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "User ID" : "UID";
		}

		private string PasswordProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "Password" : "PWD";
		}

		private IDataConnectionProperties Properties => _connectionProperties;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(OracleConnectionUIControl));
			serverLabel = new Label();
			serverTextBox = new TextBox();
			logonGroupBox = new GroupBox();
			loginTableLayoutPanel = new TableLayoutPanel();
			userNameLabel = new Label();
			userNameTextBox = new TextBox();
			passwordLabel = new Label();
			passwordTextBox = new TextBox();
			savePasswordCheckBox = new CheckBox();
			logonGroupBox.SuspendLayout();
			loginTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)serverLabel, "serverLabel");
			serverLabel.FlatStyle = FlatStyle.System;
			serverLabel.Name = "serverLabel";
			componentResourceManager.ApplyResources((object)serverTextBox, "serverTextBox");
			serverTextBox.Name = "serverTextBox";
			serverTextBox.Leave += new EventHandler(TrimControlText);
			serverTextBox.TextChanged += new EventHandler(SetServer);
			componentResourceManager.ApplyResources((object)logonGroupBox, "logonGroupBox");
			logonGroupBox.Controls.Add((Control)loginTableLayoutPanel);
			logonGroupBox.FlatStyle = FlatStyle.System;
			logonGroupBox.Name = "logonGroupBox";
			logonGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)loginTableLayoutPanel, "loginTableLayoutPanel");
			loginTableLayoutPanel.Controls.Add((Control)userNameLabel, 0, 0);
			loginTableLayoutPanel.Controls.Add((Control)userNameTextBox, 1, 0);
			loginTableLayoutPanel.Controls.Add((Control)passwordLabel, 0, 1);
			loginTableLayoutPanel.Controls.Add((Control)passwordTextBox, 1, 1);
			loginTableLayoutPanel.Controls.Add((Control)savePasswordCheckBox, 1, 2);
			loginTableLayoutPanel.Name = "loginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)userNameLabel, "userNameLabel");
			userNameLabel.FlatStyle = FlatStyle.System;
			userNameLabel.Name = "userNameLabel";
			componentResourceManager.ApplyResources((object)userNameTextBox, "userNameTextBox");
			userNameTextBox.Name = "userNameTextBox";
			userNameTextBox.Leave += new EventHandler(TrimControlText);
			userNameTextBox.TextChanged += new EventHandler(SetUserName);
			componentResourceManager.ApplyResources((object)passwordLabel, "passwordLabel");
			passwordLabel.FlatStyle = FlatStyle.System;
			passwordLabel.Name = "passwordLabel";
			componentResourceManager.ApplyResources((object)passwordTextBox, "passwordTextBox");
			passwordTextBox.Name = "passwordTextBox";
			passwordTextBox.UseSystemPasswordChar = true;
			passwordTextBox.TextChanged += new EventHandler(SetPassword);
			componentResourceManager.ApplyResources((object)savePasswordCheckBox, "savePasswordCheckBox");
			savePasswordCheckBox.Name = "savePasswordCheckBox";
			savePasswordCheckBox.CheckedChanged += new EventHandler(SetSavePassword);
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add((Control)logonGroupBox);
			Controls.Add((Control)serverTextBox);
			Controls.Add((Control)serverLabel);
			MinimumSize = new Size(300, 146);
			Name = nameof(OracleConnectionUIControl);
			logonGroupBox.ResumeLayout(false);
			logonGroupBox.PerformLayout();
			loginTableLayoutPanel.ResumeLayout(false);
			loginTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}
	}
}
