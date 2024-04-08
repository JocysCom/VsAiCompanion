using System;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public class OdbcConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private object[] _dataSourceNames;
		private Thread _uiThread;
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
			InitializeComponent();
			RightToLeft = RightToLeft.Inherit;
			dataSourceNameComboBox.AccessibleName = OdbcConnectionUIControl.TextWithoutMnemonics(useDataSourceNameRadioButton.Text);
			connectionStringTextBox.AccessibleName = OdbcConnectionUIControl.TextWithoutMnemonics(useConnectionStringRadioButton.Text);
			_uiThread = Thread.CurrentThread;
		}

		public void Initialize(IDataConnectionProperties connectionProperties)
		{
			_connectionProperties = connectionProperties is OdbcConnectionProperties ? connectionProperties : throw new ArgumentException(SR.GetString("OdbcConnectionUIControl_InvalidConnectionProperties"));
		}

		public void LoadProperties()
		{
			_loading = true;
			EnumerateDataSourceNames();
			if (Properties.ToFullString().Length == 0 || Properties["Dsn"] is string && (Properties["Dsn"] as string).Length > 0)
				useDataSourceNameRadioButton.Checked = true;
			else
				useConnectionStringRadioButton.Checked = true;
			UpdateControls();
			_loading = false;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)useDataSourceNameRadioButton);
				LayoutUtils.MirrorControl((Control)dataSourceNameTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)useConnectionStringRadioButton);
				LayoutUtils.MirrorControl((Control)connectionStringTableLayoutPanel);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)connectionStringTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)useConnectionStringRadioButton);
				LayoutUtils.UnmirrorControl((Control)dataSourceNameTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)useDataSourceNameRadioButton);
			}
		}

		protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
		{
			Size size = Size;
			MinimumSize = Size.Empty;
			base.ScaleControl(factor, specified);
			MinimumSize = new Size((int)Math.Round((double)size.Width * (double)factor.Width), (int)Math.Round((double)size.Height * (double)factor.Height));
		}

#if NETFRAMEWORK
		[UIPermission(SecurityAction.LinkDemand, Window = UIPermissionWindow.AllWindows)]
#endif
		protected override bool ProcessDialogKey(Keys keyData)
		{
			if (ActiveControl == useDataSourceNameRadioButton && (keyData & Keys.KeyCode) == Keys.Down)
			{
				useConnectionStringRadioButton.Focus();
				return true;
			}
			if (ActiveControl != useConnectionStringRadioButton || (keyData & Keys.KeyCode) != Keys.Down)
				return base.ProcessDialogKey(keyData);
			useDataSourceNameRadioButton.Focus();
			return true;
		}

		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			if (Parent != null)
				return;
			OnFontChanged(e);
		}

		private void SetDataSourceOption(object sender, EventArgs e)
		{
			if (useDataSourceNameRadioButton.Checked)
			{
				dataSourceNameTableLayoutPanel.Enabled = true;
				if (!_loading)
				{
					string property1 = Properties["Dsn"] as string;
					string property2 = Properties.Contains("uid") ? Properties["uid"] as string : (string)null;
					string property3 = Properties.Contains("pwd") ? Properties["pwd"] as string : (string)null;
					Properties.Parse(string.Empty);
					Properties["Dsn"] = (object)property1;
					Properties["uid"] = (object)property2;
					Properties["pwd"] = (object)property3;
				}
				UpdateControls();
				connectionStringTableLayoutPanel.Enabled = false;
			}
			else
			{
				dataSourceNameTableLayoutPanel.Enabled = false;
				if (!_loading)
				{
					string property4 = Properties["Dsn"] as string;
					string property5 = Properties.Contains("uid") ? Properties["uid"] as string : (string)null;
					string property6 = Properties.Contains("pwd") ? Properties["pwd"] as string : (string)null;
					Properties.Parse(connectionStringTextBox.Text);
					Properties["Dsn"] = (object)property4;
					Properties["uid"] = (object)property5;
					Properties["pwd"] = (object)property6;
				}
				UpdateControls();
				connectionStringTableLayoutPanel.Enabled = true;
			}
		}

		private void HandleComboBoxDownKey(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Down)
				return;
			EnumerateDataSourceNames(sender, (EventArgs)e);
		}

		private void EnumerateDataSourceNames(object sender, EventArgs e)
		{
			if (dataSourceNameComboBox.Items.Count != 0)
				return;
			Cursor current = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				EnumerateDataSourceNames();
			}
			finally
			{
				Cursor.Current = current;
			}
		}

		private void SetDataSourceName(object sender, EventArgs e)
		{
			if (!_loading)
				Properties["Dsn"] = dataSourceNameComboBox.Text.Length > 0 ? (object)dataSourceNameComboBox.Text : (object)(string)null;
			UpdateControls();
		}

		private void RefreshDataSourceNames(object sender, EventArgs e)
		{
			dataSourceNameComboBox.Items.Clear();
			EnumerateDataSourceNames(sender, e);
		}

		private void SetConnectionString(object sender, EventArgs e)
		{
			if (!_loading)
			{
				string property = Properties.Contains("pwd") ? Properties["pwd"] as string : (string)null;
				try
				{
					Properties.Parse(connectionStringTextBox.Text.Trim());
				}
				catch (ArgumentException ex)
				{
					IUIService uiService = (IUIService)null;
					if (ParentForm != null && ParentForm.Site != null)
						uiService = ParentForm.Site.GetService(typeof(IUIService)) as IUIService;
					if (uiService != null)
					{
						uiService.ShowError((Exception)ex);
					}
					else
					{
						int num = (int)RTLAwareMessageBox.Show((string)null, ex.Message, MessageBoxIcon.Exclamation);
					}
				}
				if (connectionStringTextBox.Text.Trim().Length > 0 && !Properties.Contains("pwd") && property != null)
					Properties["pwd"] = (object)property;
				connectionStringTextBox.Text = Properties.ToDisplayString();
			}
			UpdateControls();
		}

		private void BuildConnectionString(object sender, EventArgs e)
		{
			IntPtr EnvironmentHandle = IntPtr.Zero;
			IntPtr ConnectionHandle = IntPtr.Zero;
			try
			{
				if (!NativeMethods.SQL_SUCCEEDED(NativeMethods.SQLAllocEnv(out EnvironmentHandle)))
					throw new ApplicationException(SR.GetString("OdbcConnectionUIControl_SQLAllocEnvFailed"));
				if (!NativeMethods.SQL_SUCCEEDED(NativeMethods.SQLAllocConnect(EnvironmentHandle, out ConnectionHandle)))
					throw new ApplicationException(SR.GetString("OdbcConnectionUIControl_SQLAllocConnectFailed"));
				string fullString = Properties.ToFullString();
				StringBuilder szConnStrOut = new StringBuilder(1024);
				short pcbConnStrOut = 0;
				short rc = NativeMethods.SQLDriverConnect(ConnectionHandle, ParentForm.Handle, fullString, (short)fullString.Length, szConnStrOut, (short)1024, out pcbConnStrOut, (ushort)2);
				if (!NativeMethods.SQL_SUCCEEDED(rc) && rc != (short)100)
					rc = NativeMethods.SQLDriverConnect(ConnectionHandle, ParentForm.Handle, (string)null, (short)0, szConnStrOut, (short)1024, out pcbConnStrOut, (ushort)2);
				if (!NativeMethods.SQL_SUCCEEDED(rc) && rc != (short)100)
					throw new ApplicationException(SR.GetString("OdbcConnectionUIControl_SQLDriverConnectFailed"));
				int num = (int)NativeMethods.SQLDisconnect(ConnectionHandle);
				if (pcbConnStrOut <= (short)0)
					return;
				RefreshDataSourceNames(sender, e);
				Properties.Parse(szConnStrOut.ToString());
				UpdateControls();
			}
			finally
			{
				if (ConnectionHandle != IntPtr.Zero)
				{
					int num1 = (int)NativeMethods.SQLFreeConnect(ConnectionHandle);
				}
				if (EnvironmentHandle != IntPtr.Zero)
				{
					int num2 = (int)NativeMethods.SQLFreeEnv(EnvironmentHandle);
				}
			}
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (!_loading)
				Properties["uid"] = userNameTextBox.Text.Trim().Length > 0 ? (object)userNameTextBox.Text.Trim() : (object)(string)null;
			UpdateControls();
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (!_loading)
			{
				Properties["pwd"] = passwordTextBox.Text.Length > 0 ? (object)passwordTextBox.Text : (object)(string)null;
				passwordTextBox.Text = passwordTextBox.Text;
			}
			UpdateControls();
		}

		private void TrimControlText(object sender, EventArgs e)
		{
			Control control = sender as Control;
			control.Text = control.Text.Trim();
			UpdateControls();
		}

		private void UpdateControls()
		{
			if (Properties["Dsn"] is string && (Properties["Dsn"] as string).Length > 0 && dataSourceNameComboBox.Items.Contains(Properties["Dsn"]))
				dataSourceNameComboBox.Text = Properties["Dsn"] as string;
			else
				dataSourceNameComboBox.Text = (string)null;
			connectionStringTextBox.Text = Properties.ToDisplayString();
			if (Properties.Contains("uid"))
				userNameTextBox.Text = Properties["uid"] as string;
			else
				userNameTextBox.Text = (string)null;
			if (Properties.Contains("pwd"))
				passwordTextBox.Text = Properties["pwd"] as string;
			else
				passwordTextBox.Text = (string)null;
		}

		private void EnumerateDataSourceNames()
		{
			DataTable dataTable = new DataTable();
			dataTable.Locale = CultureInfo.InvariantCulture;
			try
			{
				OleDbDataReader enumerator = OleDbEnumerator.GetEnumerator(Type.GetTypeFromCLSID(NativeMethods.CLSID_MSDASQL_ENUMERATOR));
				using (enumerator)
					dataTable.Load((IDataReader)enumerator);
			}
			catch
			{
			}
			_dataSourceNames = new object[dataTable.Rows.Count];
			for (int index = 0; index < _dataSourceNames.Length; ++index)
				_dataSourceNames[index] = (object)(dataTable.Rows[index]["SOURCES_NAME"] as string);
			Array.Sort<object>(_dataSourceNames);
			if (Thread.CurrentThread == _uiThread)
			{
				PopulateDataSourceNameComboBox();
			}
			else
			{
				if (!IsHandleCreated)
					return;
				BeginInvoke((Delegate)new ThreadStart(PopulateDataSourceNameComboBox));
			}
		}

		private void PopulateDataSourceNameComboBox()
		{
			if (dataSourceNameComboBox.Items.Count != 0)
				return;
			if (_dataSourceNames.Length > 0)
				dataSourceNameComboBox.Items.AddRange(_dataSourceNames);
			else
				dataSourceNameComboBox.Items.Add((object)string.Empty);
		}

		private static string TextWithoutMnemonics(string text)
		{
			if (text == null)
				return (string)null;
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

		private IDataConnectionProperties Properties => _connectionProperties;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(OdbcConnectionUIControl));
			dataSourceGroupBox = new GroupBox();
			connectionStringTableLayoutPanel = new TableLayoutPanel();
			connectionStringTextBox = new TextBox();
			buildButton = new Button();
			useConnectionStringRadioButton = new RadioButton();
			dataSourceNameTableLayoutPanel = new TableLayoutPanel();
			dataSourceNameComboBox = new ComboBox();
			refreshButton = new Button();
			useDataSourceNameRadioButton = new RadioButton();
			loginGroupBox = new GroupBox();
			loginTableLayoutPanel = new TableLayoutPanel();
			userNameLabel = new Label();
			userNameTextBox = new TextBox();
			passwordLabel = new Label();
			passwordTextBox = new TextBox();
			dataSourceGroupBox.SuspendLayout();
			connectionStringTableLayoutPanel.SuspendLayout();
			dataSourceNameTableLayoutPanel.SuspendLayout();
			loginGroupBox.SuspendLayout();
			loginTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)dataSourceGroupBox, "dataSourceGroupBox");
			dataSourceGroupBox.Controls.Add((Control)connectionStringTableLayoutPanel);
			dataSourceGroupBox.Controls.Add((Control)useConnectionStringRadioButton);
			dataSourceGroupBox.Controls.Add((Control)dataSourceNameTableLayoutPanel);
			dataSourceGroupBox.Controls.Add((Control)useDataSourceNameRadioButton);
			dataSourceGroupBox.FlatStyle = FlatStyle.System;
			dataSourceGroupBox.Name = "dataSourceGroupBox";
			dataSourceGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)connectionStringTableLayoutPanel, "connectionStringTableLayoutPanel");
			connectionStringTableLayoutPanel.Controls.Add((Control)connectionStringTextBox, 0, 0);
			connectionStringTableLayoutPanel.Controls.Add((Control)buildButton, 1, 0);
			connectionStringTableLayoutPanel.Name = "connectionStringTableLayoutPanel";
			componentResourceManager.ApplyResources((object)connectionStringTextBox, "connectionStringTextBox");
			connectionStringTextBox.Name = "connectionStringTextBox";
			connectionStringTextBox.Leave += new EventHandler(SetConnectionString);
			componentResourceManager.ApplyResources((object)buildButton, "buildButton");
			buildButton.MinimumSize = new Size(75, 23);
			buildButton.Name = "buildButton";
			buildButton.Click += new EventHandler(BuildConnectionString);
			componentResourceManager.ApplyResources((object)useConnectionStringRadioButton, "useConnectionStringRadioButton");
			useConnectionStringRadioButton.Name = "useConnectionStringRadioButton";
			useConnectionStringRadioButton.CheckedChanged += new EventHandler(SetDataSourceOption);
			componentResourceManager.ApplyResources((object)dataSourceNameTableLayoutPanel, "dataSourceNameTableLayoutPanel");
			dataSourceNameTableLayoutPanel.Controls.Add((Control)dataSourceNameComboBox, 0, 0);
			dataSourceNameTableLayoutPanel.Controls.Add((Control)refreshButton, 1, 0);
			dataSourceNameTableLayoutPanel.Name = "dataSourceNameTableLayoutPanel";
			componentResourceManager.ApplyResources((object)dataSourceNameComboBox, "dataSourceNameComboBox");
			dataSourceNameComboBox.AutoCompleteMode = AutoCompleteMode.Append;
			dataSourceNameComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
			dataSourceNameComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			dataSourceNameComboBox.FormattingEnabled = true;
			dataSourceNameComboBox.Name = "dataSourceNameComboBox";
			dataSourceNameComboBox.Leave += new EventHandler(SetDataSourceName);
			dataSourceNameComboBox.SelectedIndexChanged += new EventHandler(SetDataSourceName);
			dataSourceNameComboBox.KeyDown += new KeyEventHandler(HandleComboBoxDownKey);
			dataSourceNameComboBox.DropDown += new EventHandler(EnumerateDataSourceNames);
			componentResourceManager.ApplyResources((object)refreshButton, "refreshButton");
			refreshButton.MinimumSize = new Size(75, 23);
			refreshButton.Name = "refreshButton";
			refreshButton.Click += new EventHandler(RefreshDataSourceNames);
			componentResourceManager.ApplyResources((object)useDataSourceNameRadioButton, "useDataSourceNameRadioButton");
			useDataSourceNameRadioButton.Name = "useDataSourceNameRadioButton";
			useDataSourceNameRadioButton.CheckedChanged += new EventHandler(SetDataSourceOption);
			componentResourceManager.ApplyResources((object)loginGroupBox, "loginGroupBox");
			loginGroupBox.Controls.Add((Control)loginTableLayoutPanel);
			loginGroupBox.FlatStyle = FlatStyle.System;
			loginGroupBox.Name = "loginGroupBox";
			loginGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)loginTableLayoutPanel, "loginTableLayoutPanel");
			loginTableLayoutPanel.Controls.Add((Control)userNameLabel, 0, 0);
			loginTableLayoutPanel.Controls.Add((Control)userNameTextBox, 1, 0);
			loginTableLayoutPanel.Controls.Add((Control)passwordLabel, 0, 1);
			loginTableLayoutPanel.Controls.Add((Control)passwordTextBox, 1, 1);
			loginTableLayoutPanel.Name = "loginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)userNameLabel, "userNameLabel");
			userNameLabel.FlatStyle = FlatStyle.System;
			userNameLabel.Name = "userNameLabel";
			componentResourceManager.ApplyResources((object)userNameTextBox, "userNameTextBox");
			userNameTextBox.Name = "userNameTextBox";
			userNameTextBox.Leave += new EventHandler(SetUserName);
			componentResourceManager.ApplyResources((object)passwordLabel, "passwordLabel");
			passwordLabel.FlatStyle = FlatStyle.System;
			passwordLabel.Name = "passwordLabel";
			componentResourceManager.ApplyResources((object)passwordTextBox, "passwordTextBox");
			passwordTextBox.Name = "passwordTextBox";
			passwordTextBox.UseSystemPasswordChar = true;
			passwordTextBox.Leave += new EventHandler(SetPassword);
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add((Control)loginGroupBox);
			Controls.Add((Control)dataSourceGroupBox);
			MinimumSize = new Size(350, 215);
			Name = nameof(OdbcConnectionUIControl);
			dataSourceGroupBox.ResumeLayout(false);
			dataSourceGroupBox.PerformLayout();
			connectionStringTableLayoutPanel.ResumeLayout(false);
			connectionStringTableLayoutPanel.PerformLayout();
			dataSourceNameTableLayoutPanel.ResumeLayout(false);
			dataSourceNameTableLayoutPanel.PerformLayout();
			loginGroupBox.ResumeLayout(false);
			loginGroupBox.PerformLayout();
			loginTableLayoutPanel.ResumeLayout(false);
			loginTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
		}
	}
}
