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
	public class AccessConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private IDataConnectionProperties _connectionProperties;
		private IContainer components;
		private Label databaseFileLabel;
		private TableLayoutPanel databaseFileTableLayoutPanel;
		private TextBox databaseFileTextBox;
		private Button browseButton;
		private GroupBox logonGroupBox;
		private TableLayoutPanel loginTableLayoutPanel;
		private Label userNameLabel;
		private TextBox userNameTextBox;
		private Label passwordLabel;
		private TextBox passwordTextBox;
		private CheckBox savePasswordCheckBox;

		public AccessConnectionUIControl()
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
			if (connectionProperties == null)
				throw new ArgumentNullException(nameof(connectionProperties));
			if (!(connectionProperties is OleDBAccessConnectionProperties))
				throw new ArgumentException(SR.GetString("AccessConnectionUIControl_InvalidConnectionProperties"));
			if (connectionProperties is OdbcConnectionProperties)
				savePasswordCheckBox.Enabled = false;
			_connectionProperties = connectionProperties;
		}

		public void LoadProperties()
		{
			_loading = true;
			databaseFileTextBox.Text = Properties[DatabaseFileProperty] as string;
			userNameTextBox.Text = Properties[UserNameProperty] as string;
			if (userNameTextBox.Text.Length == 0)
				userNameTextBox.Text = "Admin";
			passwordTextBox.Text = Properties[PasswordProperty] as string;
			savePasswordCheckBox.Checked = !(Properties is OdbcConnectionProperties) && (bool)Properties["Persist Security Info"];
			_loading = false;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
				LayoutUtils.MirrorControl((Control)databaseFileLabel, (Control)databaseFileTableLayoutPanel);
			else
				LayoutUtils.UnmirrorControl((Control)databaseFileLabel, (Control)databaseFileTableLayoutPanel);
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
			Properties[DatabaseFileProperty] = databaseFileTextBox.Text.Trim().Length > 0 ? (object)databaseFileTextBox.Text.Trim() : (object)(string)null;
		}

		private void Browse(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = SR.GetString("AccessConnectionUIControl_BrowseFileTitle");
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			openFileDialog.Filter = SR.GetString("AccessConnectionUIControl_BrowseFileFilter");
			openFileDialog.DefaultExt = SR.GetString("AccessConnectionUIControl_BrowseFileDefaultExt");
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

		private void SetUserName(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties[UserNameProperty] = userNameTextBox.Text.Trim().Length > 0 ? (object)userNameTextBox.Text.Trim() : (object)(string)null;
			if (!(Properties[UserNameProperty] as string).Equals("Admin"))
				return;
			Properties[UserNameProperty] = (object)null;
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

		private string DatabaseFileProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "Data Source" : "DBQ";
		}

		private string UserNameProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "User ID" : "UID";
		}

		private string PasswordProperty
		{
			get => !(Properties is OdbcConnectionProperties) ? "Jet OLEDB:Database Password" : "PWD";
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
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(AccessConnectionUIControl));
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
			databaseFileTextBox.Leave += new EventHandler(TrimControlText);
			databaseFileTextBox.TextChanged += new EventHandler(SetDatabaseFile);
			componentResourceManager.ApplyResources((object)browseButton, "browseButton");
			browseButton.Name = "browseButton";
			browseButton.Click += new EventHandler(Browse);
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
			Controls.Add((Control)databaseFileTableLayoutPanel);
			Controls.Add((Control)databaseFileLabel);
			MinimumSize = new Size(300, 148);
			Name = nameof(AccessConnectionUIControl);
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
