using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{
#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public class SqlFileConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private IDataConnectionProperties _connectionProperties;
		private IContainer components;
		private Label databaseFileLabel;
		private TableLayoutPanel databaseFileTableLayoutPanel;
		private TextBox databaseFileTextBox;
		private Button browseButton;
		private GroupBox logonGroupBox;
		private RadioButton windowsAuthenticationRadioButton;
		private RadioButton sqlAuthenticationRadioButton;
		private TableLayoutPanel loginTableLayoutPanel;
		private Label userNameLabel;
		private TextBox userNameTextBox;
		private Label passwordLabel;
		private TextBox passwordTextBox;
		private CheckBox savePasswordCheckBox;

		public SqlFileConnectionUIControl()
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
			_connectionProperties = connectionProperties is SqlFileConnectionProperties ? connectionProperties : throw new ArgumentException(SR.GetString("SqlFileConnectionUIControl_InvalidConnectionProperties"));
		}

		public void LoadProperties()
		{
			_loading = true;
			databaseFileTextBox.Text = Properties["AttachDbFilename"] as string;
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			if (databaseFileTextBox.Text.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
				databaseFileTextBox.Text = databaseFileTextBox.Text.Substring(folderPath.Length + 1);
			if ((bool)Properties["Integrated Security"])
				windowsAuthenticationRadioButton.Checked = true;
			else
				sqlAuthenticationRadioButton.Checked = true;
			userNameTextBox.Text = Properties["User ID"] as string;
			passwordTextBox.Text = Properties["Password"] as string;
			savePasswordCheckBox.Checked = (bool)Properties["Persist Security Info"];
			_loading = false;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)databaseFileLabel, (Control)databaseFileTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)windowsAuthenticationRadioButton);
				LayoutUtils.MirrorControl((Control)sqlAuthenticationRadioButton);
				LayoutUtils.MirrorControl((Control)loginTableLayoutPanel);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)loginTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)sqlAuthenticationRadioButton);
				LayoutUtils.UnmirrorControl((Control)windowsAuthenticationRadioButton);
				LayoutUtils.UnmirrorControl((Control)databaseFileLabel, (Control)databaseFileTableLayoutPanel);
			}
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

		private void SetDatabaseFile(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["AttachDbFilename"] = databaseFileTextBox.Text.Trim().Length > 0 ? (object)databaseFileTextBox.Text.Trim() : (object)(string)null;
		}

		private void UpdateDatabaseFile(object sender, EventArgs e)
		{
			if (_loading)
				return;
			string str = databaseFileTextBox.Text.Trim().Length > 0 ? databaseFileTextBox.Text.Trim() : (string)null;
			if (str != null)
			{
				if (!str.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase))
					str += ".mdf";
				try
				{
					if (!Path.IsPathRooted(str))
						str = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), str);
				}
				catch
				{
				}
			}
			Properties["AttachDbFilename"] = (object)str;
		}

		private void Browse(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = SR.GetString("SqlConnectionUIControl_BrowseFileTitle");
			openFileDialog.Multiselect = false;
			openFileDialog.CheckFileExists = false;
			openFileDialog.RestoreDirectory = true;
			openFileDialog.Filter = SR.GetString("SqlConnectionUIControl_BrowseFileFilter");
			openFileDialog.DefaultExt = SR.GetString("SqlConnectionUIControl_BrowseFileDefaultExt");
			openFileDialog.FileName = Properties["AttachDbFilename"] as string;
			if (Container != null)
				Container.Add((IComponent)openFileDialog);
			try
			{
				if (openFileDialog.ShowDialog((IWin32Window)ParentForm) != DialogResult.OK)
					return;
				databaseFileTextBox.Text = openFileDialog.FileName.Trim();
			}
			finally
			{
				if (Container != null)
					Container.Remove((IComponent)openFileDialog);
				openFileDialog.Dispose();
			}
		}

		private void SetAuthenticationOption(object sender, EventArgs e)
		{
			if (windowsAuthenticationRadioButton.Checked)
			{
				if (!_loading)
				{
					Properties["Integrated Security"] = (object)true;
					Properties.Reset("User ID");
					Properties.Reset("Password");
					Properties.Reset("Persist Security Info");
				}
				loginTableLayoutPanel.Enabled = false;
			}
			else
			{
				if (!_loading)
				{
					Properties["Integrated Security"] = (object)false;
					SetUserName(sender, e);
					SetPassword(sender, e);
					SetSavePassword(sender, e);
				}
				loginTableLayoutPanel.Enabled = true;
			}
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["User ID"] = userNameTextBox.Text.Trim().Length > 0 ? (object)userNameTextBox.Text.Trim() : (object)(string)null;
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["Password"] = passwordTextBox.Text.Length > 0 ? (object)passwordTextBox.Text : (object)(string)null;
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

		private IDataConnectionProperties Properties => _connectionProperties;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(SqlFileConnectionUIControl));
			databaseFileLabel = new Label();
			databaseFileTableLayoutPanel = new TableLayoutPanel();
			databaseFileTextBox = new TextBox();
			browseButton = new Button();
			logonGroupBox = new GroupBox();
			loginTableLayoutPanel = new TableLayoutPanel();
			userNameLabel = new Label();
			userNameTextBox = new TextBox();
			passwordLabel = new Label();
			passwordTextBox = new TextBox();
			savePasswordCheckBox = new CheckBox();
			sqlAuthenticationRadioButton = new RadioButton();
			windowsAuthenticationRadioButton = new RadioButton();
			databaseFileTableLayoutPanel.SuspendLayout();
			logonGroupBox.SuspendLayout();
			loginTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)databaseFileLabel, "databaseFileLabel");
			databaseFileLabel.FlatStyle = FlatStyle.System;
			databaseFileLabel.Name = "databaseFileLabel";
			componentResourceManager.ApplyResources((object)databaseFileTableLayoutPanel, "databaseFileTableLayoutPanel");
			databaseFileTableLayoutPanel.Controls.Add((Control)databaseFileTextBox, 0, 0);
			databaseFileTableLayoutPanel.Controls.Add((Control)browseButton, 1, 0);
			databaseFileTableLayoutPanel.Name = "databaseFileTableLayoutPanel";
			componentResourceManager.ApplyResources((object)databaseFileTextBox, "databaseFileTextBox");
			databaseFileTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
			databaseFileTextBox.AutoCompleteSource = AutoCompleteSource.FileSystem;
			databaseFileTextBox.Name = "databaseFileTextBox";
			databaseFileTextBox.Leave += new EventHandler(UpdateDatabaseFile);
			databaseFileTextBox.TextChanged += new EventHandler(SetDatabaseFile);
			componentResourceManager.ApplyResources((object)browseButton, "browseButton");
			browseButton.Name = "browseButton";
			browseButton.Click += new EventHandler(Browse);
			componentResourceManager.ApplyResources((object)logonGroupBox, "logonGroupBox");
			logonGroupBox.Controls.Add((Control)loginTableLayoutPanel);
			logonGroupBox.Controls.Add((Control)sqlAuthenticationRadioButton);
			logonGroupBox.Controls.Add((Control)windowsAuthenticationRadioButton);
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
			componentResourceManager.ApplyResources((object)sqlAuthenticationRadioButton, "sqlAuthenticationRadioButton");
			sqlAuthenticationRadioButton.Name = "sqlAuthenticationRadioButton";
			sqlAuthenticationRadioButton.CheckedChanged += new EventHandler(SetAuthenticationOption);
			componentResourceManager.ApplyResources((object)windowsAuthenticationRadioButton, "windowsAuthenticationRadioButton");
			windowsAuthenticationRadioButton.Name = "windowsAuthenticationRadioButton";
			windowsAuthenticationRadioButton.CheckedChanged += new EventHandler(SetAuthenticationOption);
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add((Control)logonGroupBox);
			Controls.Add((Control)databaseFileTableLayoutPanel);
			Controls.Add((Control)databaseFileLabel);
			MinimumSize = new Size(300, 191);
			Name = nameof(SqlFileConnectionUIControl);
			databaseFileTableLayoutPanel.ResumeLayout(false);
			databaseFileTableLayoutPanel.PerformLayout();
			logonGroupBox.ResumeLayout(false);
			logonGroupBox.PerformLayout();
			loginTableLayoutPanel.ResumeLayout(false);
			loginTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}
	}
}
