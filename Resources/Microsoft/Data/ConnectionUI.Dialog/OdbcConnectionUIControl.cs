using System;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Design;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OdbcConnectionUIControl : UserControl, IDataConnectionUIControl
  {
    private bool _loading;
    private object[] _dataSourceNames;
    private Thread _uiThread;
    private Thread _dataSourceNameEnumerationThread;
    private IDataConnectionProperties _connectionProperties;
    private IContainer components;
    private GroupBox dataSourceGroupBox;
    private RadioButton useDataSourceNameRadioButton;
    private TableLayoutPanel dataSourceNameTableLayoutPanel;
    private ComboBox dataSourceNameComboBox;
    private Button refreshButton;
    private RadioButton useConnectionStringRadioButton;
    private TableLayoutPanel connectionStringTableLayoutPanel;
    private TextBox connectionStringTextBox;
    private Button buildButton;
    private GroupBox loginGroupBox;
    private TableLayoutPanel loginTableLayoutPanel;
    private Label userNameLabel;
    private TextBox userNameTextBox;
    private Label passwordLabel;
    private TextBox passwordTextBox;

    public OdbcConnectionUIControl()
    {
      this.InitializeComponent();
      this.RightToLeft = RightToLeft.Inherit;
      this.dataSourceNameComboBox.AccessibleName = OdbcConnectionUIControl.TextWithoutMnemonics(this.useDataSourceNameRadioButton.Text);
      this.connectionStringTextBox.AccessibleName = OdbcConnectionUIControl.TextWithoutMnemonics(this.useConnectionStringRadioButton.Text);
      this._uiThread = Thread.CurrentThread;
    }

    public void Initialize(IDataConnectionProperties connectionProperties)
    {
      this._connectionProperties = connectionProperties is OdbcConnectionProperties ? connectionProperties : throw new ArgumentException(SR.OdbcConnectionUIControl_InvalidConnectionProperties);
    }

    public void LoadProperties()
    {
      this._loading = true;
      this.EnumerateDataSourceNames();
      if (this.Properties.ToFullString().Length == 0 || this.Properties["Dsn"] is string && (this.Properties["Dsn"] as string).Length > 0)
        this.useDataSourceNameRadioButton.Checked = true;
      else
        this.useConnectionStringRadioButton.Checked = true;
      this.UpdateControls();
      this._loading = false;
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
      base.OnRightToLeftChanged(e);
      if (this.ParentForm != null && this.ParentForm.RightToLeftLayout && this.RightToLeft == RightToLeft.Yes)
      {
        LayoutUtils.MirrorControl((Control) this.useDataSourceNameRadioButton);
        LayoutUtils.MirrorControl((Control) this.dataSourceNameTableLayoutPanel);
        LayoutUtils.MirrorControl((Control) this.useConnectionStringRadioButton);
        LayoutUtils.MirrorControl((Control) this.connectionStringTableLayoutPanel);
      }
      else
      {
        LayoutUtils.UnmirrorControl((Control) this.connectionStringTableLayoutPanel);
        LayoutUtils.UnmirrorControl((Control) this.useConnectionStringRadioButton);
        LayoutUtils.UnmirrorControl((Control) this.dataSourceNameTableLayoutPanel);
        LayoutUtils.UnmirrorControl((Control) this.useDataSourceNameRadioButton);
      }
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
      Size size = this.Size;
      this.MinimumSize = Size.Empty;
      base.ScaleControl(factor, specified);
      this.MinimumSize = new Size((int) Math.Round((double) size.Width * (double) factor.Width), (int) Math.Round((double) size.Height * (double) factor.Height));
    }

    protected override bool ProcessDialogKey(System.Windows.Forms.Keys keyData)
    {
      if (this.ActiveControl == this.useDataSourceNameRadioButton && (keyData & System.Windows.Forms.Keys.KeyCode) == System.Windows.Forms.Keys.Down)
      {
        this.useConnectionStringRadioButton.Focus();
        return true;
      }
      if (this.ActiveControl != this.useConnectionStringRadioButton || (keyData & System.Windows.Forms.Keys.KeyCode) != System.Windows.Forms.Keys.Down)
        return base.ProcessDialogKey(keyData);
      this.useDataSourceNameRadioButton.Focus();
      return true;
    }

    protected override void OnParentChanged(EventArgs e)
    {
      base.OnParentChanged(e);
      if (this.Parent != null)
        return;
      this.OnFontChanged(e);
    }

    private void SetDataSourceOption(object sender, EventArgs e)
    {
      if (this.useDataSourceNameRadioButton.Checked)
      {
        this.dataSourceNameTableLayoutPanel.Enabled = true;
        if (!this._loading)
        {
          string property1 = this.Properties["Dsn"] as string;
          string property2 = this.Properties.Contains("uid") ? this.Properties["uid"] as string : (string) null;
          string property3 = this.Properties.Contains("pwd") ? this.Properties["pwd"] as string : (string) null;
          this.Properties.Parse(string.Empty);
          this.Properties["Dsn"] = (object) property1;
          this.Properties["uid"] = (object) property2;
          this.Properties["pwd"] = (object) property3;
        }
        this.UpdateControls();
        this.connectionStringTableLayoutPanel.Enabled = false;
      }
      else
      {
        this.dataSourceNameTableLayoutPanel.Enabled = false;
        if (!this._loading)
        {
          string property4 = this.Properties["Dsn"] as string;
          string property5 = this.Properties.Contains("uid") ? this.Properties["uid"] as string : (string) null;
          string property6 = this.Properties.Contains("pwd") ? this.Properties["pwd"] as string : (string) null;
          this.Properties.Parse(this.connectionStringTextBox.Text);
          this.Properties["Dsn"] = (object) property4;
          this.Properties["uid"] = (object) property5;
          this.Properties["pwd"] = (object) property6;
        }
        this.UpdateControls();
        this.connectionStringTableLayoutPanel.Enabled = true;
      }
    }

    private void HandleComboBoxDownKey(object sender, KeyEventArgs e)
    {
      if (e.KeyCode != System.Windows.Forms.Keys.Down)
        return;
      this.EnumerateDataSourceNames(sender, (EventArgs) e);
    }

    private void EnumerateDataSourceNames(object sender, EventArgs e)
    {
      if (this.dataSourceNameComboBox.Items.Count != 0)
        return;
      Cursor current = Cursor.Current;
      Cursor.Current = Cursors.WaitCursor;
      try
      {
        if (this._dataSourceNameEnumerationThread == null || this._dataSourceNameEnumerationThread.ThreadState == ThreadState.Stopped)
        {
          this.EnumerateDataSourceNames();
        }
        else
        {
          if (this._dataSourceNameEnumerationThread.ThreadState != ThreadState.Running)
            return;
          this._dataSourceNameEnumerationThread.Join();
          this.PopulateDataSourceNameComboBox();
        }
      }
      finally
      {
        Cursor.Current = current;
      }
    }

    private void SetDataSourceName(object sender, EventArgs e)
    {
      if (!this._loading)
        this.Properties["Dsn"] = this.dataSourceNameComboBox.Text.Length > 0 ? (object) this.dataSourceNameComboBox.Text : (object) (string) null;
      this.UpdateControls();
    }

    private void RefreshDataSourceNames(object sender, EventArgs e)
    {
      this.dataSourceNameComboBox.Items.Clear();
      this.EnumerateDataSourceNames(sender, e);
    }

    private void SetConnectionString(object sender, EventArgs e)
    {
      if (!this._loading)
      {
        string property = this.Properties.Contains("pwd") ? this.Properties["pwd"] as string : (string) null;
        try
        {
          this.Properties.Parse(this.connectionStringTextBox.Text.Trim());
        }
        catch (ArgumentException ex)
        {
          IUIService uiService = (IUIService) null;
          if (this.ParentForm != null && this.ParentForm.Site != null)
            uiService = this.ParentForm.Site.GetService(typeof (IUIService)) as IUIService;
          if (uiService != null)
          {
            uiService.ShowError((Exception) ex);
          }
          else
          {
            int num = (int) RTLAwareMessageBox.Show((string) null, ex.Message, MessageBoxIcon.Exclamation);
          }
        }
        if (this.connectionStringTextBox.Text.Trim().Length > 0 && !this.Properties.Contains("pwd") && property != null)
          this.Properties["pwd"] = (object) property;
        this.connectionStringTextBox.Text = this.Properties.ToDisplayString();
      }
      this.UpdateControls();
    }

    private void BuildConnectionString(object sender, EventArgs e)
    {
      IntPtr EnvironmentHandle = IntPtr.Zero;
      IntPtr ConnectionHandle = IntPtr.Zero;
      try
      {
        if (!NativeMethods.SQL_SUCCEEDED(NativeMethods.SQLAllocEnv(out EnvironmentHandle)))
          throw new ApplicationException(SR.OdbcConnectionUIControl_SQLAllocEnvFailed);
        if (!NativeMethods.SQL_SUCCEEDED(NativeMethods.SQLAllocConnect(EnvironmentHandle, out ConnectionHandle)))
          throw new ApplicationException(SR.OdbcConnectionUIControl_SQLAllocConnectFailed);
        string fullString = this.Properties.ToFullString();
        StringBuilder szConnStrOut = new StringBuilder(1024);
        short pcbConnStrOut = 0;
        short rc = NativeMethods.SQLDriverConnect(ConnectionHandle, this.ParentForm.Handle, fullString, (short) fullString.Length, szConnStrOut, (short) 1024, out pcbConnStrOut, (ushort) 2);
        if (!NativeMethods.SQL_SUCCEEDED(rc) && rc != (short) 100)
          rc = NativeMethods.SQLDriverConnect(ConnectionHandle, this.ParentForm.Handle, (string) null, (short) 0, szConnStrOut, (short) 1024, out pcbConnStrOut, (ushort) 2);
        if (!NativeMethods.SQL_SUCCEEDED(rc) && rc != (short) 100)
          throw new ApplicationException(SR.OdbcConnectionUIControl_SQLDriverConnectFailed);
        int num = (int) NativeMethods.SQLDisconnect(ConnectionHandle);
        if (pcbConnStrOut <= (short) 0)
          return;
        this.RefreshDataSourceNames(sender, e);
        this.Properties.Parse(szConnStrOut.ToString());
        this.UpdateControls();
      }
      finally
      {
        if (ConnectionHandle != IntPtr.Zero)
        {
          int num1 = (int) NativeMethods.SQLFreeConnect(ConnectionHandle);
        }
        if (EnvironmentHandle != IntPtr.Zero)
        {
          int num2 = (int) NativeMethods.SQLFreeEnv(EnvironmentHandle);
        }
      }
    }

    private void SetUserName(object sender, EventArgs e)
    {
      if (!this._loading)
        this.Properties["uid"] = this.userNameTextBox.Text.Trim().Length > 0 ? (object) this.userNameTextBox.Text.Trim() : (object) (string) null;
      this.UpdateControls();
    }

    private void SetPassword(object sender, EventArgs e)
    {
      if (!this._loading)
      {
        this.Properties["pwd"] = this.passwordTextBox.Text.Length > 0 ? (object) this.passwordTextBox.Text : (object) (string) null;
        this.passwordTextBox.Text = this.passwordTextBox.Text;
      }
      this.UpdateControls();
    }

    private void TrimControlText(object sender, EventArgs e)
    {
      Control control = sender as Control;
      control.Text = control.Text.Trim();
      this.UpdateControls();
    }

    private void UpdateControls()
    {
      if (this.Properties["Dsn"] is string && (this.Properties["Dsn"] as string).Length > 0 && this.dataSourceNameComboBox.Items.Contains(this.Properties["Dsn"]))
        this.dataSourceNameComboBox.Text = this.Properties["Dsn"] as string;
      else
        this.dataSourceNameComboBox.Text = (string) null;
      this.connectionStringTextBox.Text = this.Properties.ToDisplayString();
      if (this.Properties.Contains("uid"))
        this.userNameTextBox.Text = this.Properties["uid"] as string;
      else
        this.userNameTextBox.Text = (string) null;
      if (this.Properties.Contains("pwd"))
        this.passwordTextBox.Text = this.Properties["pwd"] as string;
      else
        this.passwordTextBox.Text = (string) null;
    }

    private void EnumerateDataSourceNames()
    {
      DataTable dataTable = new DataTable();
      dataTable.Locale = CultureInfo.InvariantCulture;
      try
      {
        OleDbDataReader enumerator = OleDbEnumerator.GetEnumerator(System.Type.GetTypeFromCLSID(NativeMethods.CLSID_MSDASQL_ENUMERATOR));
        using (enumerator)
          dataTable.Load((IDataReader) enumerator);
      }
      catch
      {
      }
      this._dataSourceNames = new object[dataTable.Rows.Count];
      for (int index = 0; index < this._dataSourceNames.Length; ++index)
        this._dataSourceNames[index] = (object) (dataTable.Rows[index]["SOURCES_NAME"] as string);
      Array.Sort<object>(this._dataSourceNames);
      if (Thread.CurrentThread == this._uiThread)
      {
        this.PopulateDataSourceNameComboBox();
      }
      else
      {
        if (!this.IsHandleCreated)
          return;
        this.BeginInvoke((Delegate) new ThreadStart(this.PopulateDataSourceNameComboBox));
      }
    }

    private void PopulateDataSourceNameComboBox()
    {
      if (this.dataSourceNameComboBox.Items.Count != 0)
        return;
      if (this._dataSourceNames.Length != 0)
        this.dataSourceNameComboBox.Items.AddRange(this._dataSourceNames);
      else
        this.dataSourceNameComboBox.Items.Add((object) string.Empty);
    }

    private static string TextWithoutMnemonics(string text)
    {
      if (text == null)
        return (string) null;
      int num = text.IndexOf('&');
      if (num == -1)
        return text;
      StringBuilder stringBuilder = new StringBuilder(text.Substring(0, num));
      for (; num < text.Length; ++num)
      {
        if (text[num] == '&')
          ++num;
        if (num < text.Length)
          stringBuilder.Append(text[num]);
      }
      return stringBuilder.ToString();
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
      ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof (OdbcConnectionUIControl));
      this.dataSourceGroupBox = new GroupBox();
      this.connectionStringTableLayoutPanel = new TableLayoutPanel();
      this.connectionStringTextBox = new TextBox();
      this.buildButton = new Button();
      this.useConnectionStringRadioButton = new RadioButton();
      this.dataSourceNameTableLayoutPanel = new TableLayoutPanel();
      this.dataSourceNameComboBox = new ComboBox();
      this.refreshButton = new Button();
      this.useDataSourceNameRadioButton = new RadioButton();
      this.loginGroupBox = new GroupBox();
      this.loginTableLayoutPanel = new TableLayoutPanel();
      this.userNameLabel = new Label();
      this.userNameTextBox = new TextBox();
      this.passwordLabel = new Label();
      this.passwordTextBox = new TextBox();
      this.dataSourceGroupBox.SuspendLayout();
      this.connectionStringTableLayoutPanel.SuspendLayout();
      this.dataSourceNameTableLayoutPanel.SuspendLayout();
      this.loginGroupBox.SuspendLayout();
      this.loginTableLayoutPanel.SuspendLayout();
      this.SuspendLayout();
      componentResourceManager.ApplyResources((object) this.dataSourceGroupBox, "dataSourceGroupBox");
      this.dataSourceGroupBox.Controls.Add((Control) this.connectionStringTableLayoutPanel);
      this.dataSourceGroupBox.Controls.Add((Control) this.useConnectionStringRadioButton);
      this.dataSourceGroupBox.Controls.Add((Control) this.dataSourceNameTableLayoutPanel);
      this.dataSourceGroupBox.Controls.Add((Control) this.useDataSourceNameRadioButton);
      this.dataSourceGroupBox.FlatStyle = FlatStyle.System;
      this.dataSourceGroupBox.Name = "dataSourceGroupBox";
      this.dataSourceGroupBox.TabStop = false;
      componentResourceManager.ApplyResources((object) this.connectionStringTableLayoutPanel, "connectionStringTableLayoutPanel");
      this.connectionStringTableLayoutPanel.Controls.Add((Control) this.connectionStringTextBox, 0, 0);
      this.connectionStringTableLayoutPanel.Controls.Add((Control) this.buildButton, 1, 0);
      this.connectionStringTableLayoutPanel.Name = "connectionStringTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.connectionStringTextBox, "connectionStringTextBox");
      this.connectionStringTextBox.Name = "connectionStringTextBox";
      this.connectionStringTextBox.Leave += new EventHandler(this.SetConnectionString);
      componentResourceManager.ApplyResources((object) this.buildButton, "buildButton");
      this.buildButton.MinimumSize = new Size(75, 23);
      this.buildButton.Name = "buildButton";
      this.buildButton.Click += new EventHandler(this.BuildConnectionString);
      componentResourceManager.ApplyResources((object) this.useConnectionStringRadioButton, "useConnectionStringRadioButton");
      this.useConnectionStringRadioButton.Name = "useConnectionStringRadioButton";
      this.useConnectionStringRadioButton.CheckedChanged += new EventHandler(this.SetDataSourceOption);
      componentResourceManager.ApplyResources((object) this.dataSourceNameTableLayoutPanel, "dataSourceNameTableLayoutPanel");
      this.dataSourceNameTableLayoutPanel.Controls.Add((Control) this.dataSourceNameComboBox, 0, 0);
      this.dataSourceNameTableLayoutPanel.Controls.Add((Control) this.refreshButton, 1, 0);
      this.dataSourceNameTableLayoutPanel.Name = "dataSourceNameTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.dataSourceNameComboBox, "dataSourceNameComboBox");
      this.dataSourceNameComboBox.AutoCompleteMode = AutoCompleteMode.Append;
      this.dataSourceNameComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
      this.dataSourceNameComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
      this.dataSourceNameComboBox.FormattingEnabled = true;
      this.dataSourceNameComboBox.Name = "dataSourceNameComboBox";
      this.dataSourceNameComboBox.Leave += new EventHandler(this.SetDataSourceName);
      this.dataSourceNameComboBox.SelectedIndexChanged += new EventHandler(this.SetDataSourceName);
      this.dataSourceNameComboBox.KeyDown += new KeyEventHandler(this.HandleComboBoxDownKey);
      this.dataSourceNameComboBox.DropDown += new EventHandler(this.EnumerateDataSourceNames);
      componentResourceManager.ApplyResources((object) this.refreshButton, "refreshButton");
      this.refreshButton.MinimumSize = new Size(75, 23);
      this.refreshButton.Name = "refreshButton";
      this.refreshButton.Click += new EventHandler(this.RefreshDataSourceNames);
      componentResourceManager.ApplyResources((object) this.useDataSourceNameRadioButton, "useDataSourceNameRadioButton");
      this.useDataSourceNameRadioButton.Name = "useDataSourceNameRadioButton";
      this.useDataSourceNameRadioButton.CheckedChanged += new EventHandler(this.SetDataSourceOption);
      componentResourceManager.ApplyResources((object) this.loginGroupBox, "loginGroupBox");
      this.loginGroupBox.Controls.Add((Control) this.loginTableLayoutPanel);
      this.loginGroupBox.FlatStyle = FlatStyle.System;
      this.loginGroupBox.Name = "loginGroupBox";
      this.loginGroupBox.TabStop = false;
      componentResourceManager.ApplyResources((object) this.loginTableLayoutPanel, "loginTableLayoutPanel");
      this.loginTableLayoutPanel.Controls.Add((Control) this.userNameLabel, 0, 0);
      this.loginTableLayoutPanel.Controls.Add((Control) this.userNameTextBox, 1, 0);
      this.loginTableLayoutPanel.Controls.Add((Control) this.passwordLabel, 0, 1);
      this.loginTableLayoutPanel.Controls.Add((Control) this.passwordTextBox, 1, 1);
      this.loginTableLayoutPanel.Name = "loginTableLayoutPanel";
      componentResourceManager.ApplyResources((object) this.userNameLabel, "userNameLabel");
      this.userNameLabel.FlatStyle = FlatStyle.System;
      this.userNameLabel.Name = "userNameLabel";
      componentResourceManager.ApplyResources((object) this.userNameTextBox, "userNameTextBox");
      this.userNameTextBox.Name = "userNameTextBox";
      this.userNameTextBox.Leave += new EventHandler(this.SetUserName);
      componentResourceManager.ApplyResources((object) this.passwordLabel, "passwordLabel");
      this.passwordLabel.FlatStyle = FlatStyle.System;
      this.passwordLabel.Name = "passwordLabel";
      componentResourceManager.ApplyResources((object) this.passwordTextBox, "passwordTextBox");
      this.passwordTextBox.Name = "passwordTextBox";
      this.passwordTextBox.UseSystemPasswordChar = true;
      this.passwordTextBox.Leave += new EventHandler(this.SetPassword);
      componentResourceManager.ApplyResources((object) this, "$this");
      this.AutoScaleMode = AutoScaleMode.Font;
      this.Controls.Add((Control) this.loginGroupBox);
      this.Controls.Add((Control) this.dataSourceGroupBox);
      this.MinimumSize = new Size(350, 215);
      this.Name = nameof (OdbcConnectionUIControl);
      this.dataSourceGroupBox.ResumeLayout(false);
      this.dataSourceGroupBox.PerformLayout();
      this.connectionStringTableLayoutPanel.ResumeLayout(false);
      this.connectionStringTableLayoutPanel.PerformLayout();
      this.dataSourceNameTableLayoutPanel.ResumeLayout(false);
      this.dataSourceNameTableLayoutPanel.PerformLayout();
      this.loginGroupBox.ResumeLayout(false);
      this.loginGroupBox.PerformLayout();
      this.loginTableLayoutPanel.ResumeLayout(false);
      this.loginTableLayoutPanel.PerformLayout();
      this.ResumeLayout(false);
    }
  }
}
