using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
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
      this.InitializeComponent();
      this.RightToLeft = RightToLeft.Inherit;
    }

    public void Initialize(IDataConnectionProperties connectionProperties)
    {
      if (connectionProperties == null)
        throw new ArgumentNullException(nameof (connectionProperties));
      if (!(connectionProperties is OleDBAccessConnectionProperties))
        throw new ArgumentException(SR.AccessConnectionUIControl_InvalidConnectionProperties);
      if (connectionProperties is OdbcConnectionProperties)
        this.savePasswordCheckBox.Enabled = false;
      this._connectionProperties = connectionProperties;
    }

    public void LoadProperties()
    {
      this._loading = true;
      this.databaseFileTextBox.Text = this.Properties[this.DatabaseFileProperty] as string;
      this.userNameTextBox.Text = this.Properties[this.UserNameProperty] as string;
      if (this.userNameTextBox.Text.Length == 0)
        this.userNameTextBox.Text = "Admin";
      this.passwordTextBox.Text = this.Properties[this.PasswordProperty] as string;
      this.savePasswordCheckBox.Checked = !(this.Properties is OdbcConnectionProperties) && (bool) this.Properties["Persist Security Info"];
      this._loading = false;
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
      base.OnRightToLeftChanged(e);
      if (this.ParentForm != null && this.ParentForm.RightToLeftLayout && this.RightToLeft == RightToLeft.Yes)
        LayoutUtils.MirrorControl((Control) this.databaseFileLabel, (Control) this.databaseFileTableLayoutPanel);
      else
        LayoutUtils.UnmirrorControl((Control) this.databaseFileLabel, (Control) this.databaseFileTableLayoutPanel);
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
      Size size = this.Size;
      this.MinimumSize = Size.Empty;
      base.ScaleControl(factor, specified);
      this.MinimumSize = new Size((int) Math.Round((double) size.Width * (double) factor.Width), (int) Math.Round((double) size.Height * (double) factor.Height));
    }

    protected override void OnParentChanged(EventArgs e)
    {
      base.OnParentChanged(e);
      if (this.Parent != null)
        return;
      this.OnFontChanged(e);
    }

    private void SetDatabaseFile(object sender, EventArgs e)
    {
      if (this._loading)
        return;
      this.Properties[this.DatabaseFileProperty] = this.databaseFileTextBox.Text.Trim().Length > 0 ? (object) this.databaseFileTextBox.Text.Trim() : (object) (string) null;
    }

    private void Browse(object sender, EventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Title = SR.AccessConnectionUIControl_BrowseFileTitle;
      openFileDialog.Multiselect = false;
      openFileDialog.RestoreDirectory = true;
      openFileDialog.Filter = SR.AccessConnectionUIControl_BrowseFileFilter;
      openFileDialog.DefaultExt = SR.AccessConnectionUIControl_BrowseFileDefaultExt;
      if (this.Container != null)
        this.Container.Add((IComponent) openFileDialog);
      try
      {
        if (openFileDialog.ShowDialog((IWin32Window) this.ParentForm) != DialogResult.OK)
          return;
        this.databaseFileTextBox.Text = openFileDialog.FileName.Trim();
      }
      finally
      {
        if (this.Container != null)
          this.Container.Remove((IComponent) openFileDialog);
        openFileDialog.Dispose();
      }
    }

    private void SetUserName(object sender, EventArgs e)
    {
      if (this._loading)
        return;
      this.Properties[this.UserNameProperty] = this.userNameTextBox.Text.Trim().Length > 0 ? (object) this.userNameTextBox.Text.Trim() : (object) (string) null;
      if (!(this.Properties[this.UserNameProperty] as string).Equals("Admin"))
        return;
      this.Properties[this.UserNameProperty] = (object) null;
    }

    private void SetPassword(object sender, EventArgs e)
    {
      if (this._loading)
        return;
      this.Properties[this.PasswordProperty] = this.passwordTextBox.Text.Length > 0 ? (object) this.passwordTextBox.Text : (object) (string) null;
      this.passwordTextBox.Text = this.passwordTextBox.Text;
    }

    private void SetSavePassword(object sender, EventArgs e)
    {
      if (this._loading)
        return;
      this.Properties["Persist Security Info"] = (object) this.savePasswordCheckBox.Checked;
    }

    private void TrimControlText(object sender, EventArgs e)
    {
      Control control = sender as Control;
      control.Text = control.Text.Trim();
    }

    private string DatabaseFileProperty
    {
      get => !(this.Properties is OdbcConnectionProperties) ? "Data Source" : "DBQ";
    }

    private string UserNameProperty
    {
      get => !(this.Properties is OdbcConnectionProperties) ? "User ID" : "UID";
    }

    private string PasswordProperty
    {
      get => !(this.Properties is OdbcConnectionProperties) ? "Jet OLEDB:Database Password" : "PWD";
    }

    private IDataConnectionProperties Properties => this._connectionProperties;

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (AccessConnectionUIControl));
      this.databaseFileLabel = new Label();
      this.databaseFileTableLayoutPanel = new TableLayoutPanel();
      this.databaseFileTextBox = new TextBox();
      this.browseButton = new Button();
      this.logonGroupBox = new GroupBox();
      this.loginTableLayoutPanel = new TableLayoutPanel();
      this.userNameLabel = new Label();
      this.userNameTextBox = new TextBox();
      this.passwordLabel = new Label();
      this.passwordTextBox = new TextBox();
      this.savePasswordCheckBox = new CheckBox();
      this.databaseFileTableLayoutPanel.SuspendLayout();
      this.logonGroupBox.SuspendLayout();
      this.loginTableLayoutPanel.SuspendLayout();
      this.SuspendLayout();
      componentResourceManager.ApplyResources((object) this.databaseFileLabel, "databaseFileLabel");
      this.databaseFileLabel.FlatStyle = FlatStyle.System;
      this.databaseFileLabel.Name = "databaseFileLabel";
      componentResourceManager.ApplyResources((object) this.databaseFileTableLayoutPanel, "databaseFileTableLayoutPanel");
      this.databaseFileTableLayoutPanel.Controls.Add((Control) this.databaseFileTextBox, 0, 0);
      this.databaseFileTableLayoutPanel.Controls.Add((Control) this.browseButton, 1, 0);
      this.databaseFileTableLayoutPanel.Name = "databaseFileTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.databaseFileTextBox, "databaseFileTextBox");
      this.databaseFileTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
      this.databaseFileTextBox.AutoCompleteSource = AutoCompleteSource.FileSystem;
      this.databaseFileTextBox.Name = "databaseFileTextBox";
      this.databaseFileTextBox.Leave += new EventHandler(this.TrimControlText);
      this.databaseFileTextBox.TextChanged += new EventHandler(this.SetDatabaseFile);
      componentResourceManager.ApplyResources((object) this.browseButton, "browseButton");
      this.browseButton.Name = "browseButton";
      this.browseButton.Click += new EventHandler(this.Browse);
      componentResourceManager.ApplyResources((object) this.logonGroupBox, "logonGroupBox");
      this.logonGroupBox.Controls.Add((Control) this.loginTableLayoutPanel);
      this.logonGroupBox.FlatStyle = FlatStyle.System;
      this.logonGroupBox.Name = "logonGroupBox";
      this.logonGroupBox.TabStop = false;
      componentResourceManager.ApplyResources((object) this.loginTableLayoutPanel, "loginTableLayoutPanel");
      this.loginTableLayoutPanel.Controls.Add((Control) this.userNameLabel, 0, 0);
      this.loginTableLayoutPanel.Controls.Add((Control) this.userNameTextBox, 1, 0);
      this.loginTableLayoutPanel.Controls.Add((Control) this.passwordLabel, 0, 1);
      this.loginTableLayoutPanel.Controls.Add((Control) this.passwordTextBox, 1, 1);
      this.loginTableLayoutPanel.Controls.Add((Control) this.savePasswordCheckBox, 1, 2);
      this.loginTableLayoutPanel.Name = "loginTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.userNameLabel, "userNameLabel");
      this.userNameLabel.FlatStyle = FlatStyle.System;
      this.userNameLabel.Name = "userNameLabel";
      componentResourceManager.ApplyResources((object) this.userNameTextBox, "userNameTextBox");
      this.userNameTextBox.Name = "userNameTextBox";
      this.userNameTextBox.Leave += new EventHandler(this.TrimControlText);
      this.userNameTextBox.TextChanged += new EventHandler(this.SetUserName);
      componentResourceManager.ApplyResources((object) this.passwordLabel, "passwordLabel");
      this.passwordLabel.FlatStyle = FlatStyle.System;
      this.passwordLabel.Name = "passwordLabel";
      componentResourceManager.ApplyResources((object) this.passwordTextBox, "passwordTextBox");
      this.passwordTextBox.Name = "passwordTextBox";
      this.passwordTextBox.UseSystemPasswordChar = true;
      this.passwordTextBox.TextChanged += new EventHandler(this.SetPassword);
      componentResourceManager.ApplyResources((object) this.savePasswordCheckBox, "savePasswordCheckBox");
      this.savePasswordCheckBox.Name = "savePasswordCheckBox";
      this.savePasswordCheckBox.CheckedChanged += new EventHandler(this.SetSavePassword);
      componentResourceManager.ApplyResources((object) this, "$this");
      this.AutoScaleMode = AutoScaleMode.Font;
      this.Controls.Add((Control) this.logonGroupBox);
      this.Controls.Add((Control) this.databaseFileTableLayoutPanel);
      this.Controls.Add((Control) this.databaseFileLabel);
      this.MinimumSize = new Size(300, 148);
      this.Name = nameof (AccessConnectionUIControl);
      this.databaseFileTableLayoutPanel.ResumeLayout(false);
      this.databaseFileTableLayoutPanel.PerformLayout();
      this.logonGroupBox.ResumeLayout(false);
      this.logonGroupBox.PerformLayout();
      this.loginTableLayoutPanel.ResumeLayout(false);
      this.loginTableLayoutPanel.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
