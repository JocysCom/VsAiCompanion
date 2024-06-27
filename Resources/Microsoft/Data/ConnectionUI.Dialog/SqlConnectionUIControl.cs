using System;
using System.ComponentModel;
using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public class SqlConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private object[] _servers;
		private object[] _databases;
		private Thread _uiThread;
		private Thread _serverEnumerationThread;
		private Thread _databaseEnumerationThread;
		private string currentOleDBProvider;
		private bool currentUserInstanceSetting;
		private SqlConnectionUIControl.ControlProperties _controlProperties;
		private IContainer components;
		private Label serverLabel;
		private TableLayoutPanel serverTableLayoutPanel;
		private ComboBox serverComboBox;
		private Button refreshButton;
		private GroupBox logonGroupBox;
		private RadioButton windowsAuthenticationRadioButton;
		private RadioButton sqlAuthenticationRadioButton;
		private TableLayoutPanel loginTableLayoutPanel;
		private Label userNameLabel;
		private TextBox userNameTextBox;
		private Label passwordLabel;
		private TextBox passwordTextBox;
		private CheckBox savePasswordCheckBox;
		private GroupBox databaseGroupBox;
		private RadioButton selectDatabaseRadioButton;
		private ComboBox selectDatabaseComboBox;
		private RadioButton attachDatabaseRadioButton;
		private TableLayoutPanel attachDatabaseTableLayoutPanel;
		private TextBox attachDatabaseTextBox;
		private Button browseButton;
		private Label logicalDatabaseNameLabel;
		private TextBox logicalDatabaseNameTextBox;

		public SqlConnectionUIControl()
		{
			InitializeComponent();
			RightToLeft = RightToLeft.Inherit;
			if (savePasswordCheckBox.Height < LayoutUtils.GetPreferredCheckBoxHeight(savePasswordCheckBox))
			{
				savePasswordCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
				loginTableLayoutPanel.Height += loginTableLayoutPanel.Margin.Bottom;
				loginTableLayoutPanel.Margin = new Padding(loginTableLayoutPanel.Margin.Left, loginTableLayoutPanel.Margin.Top, loginTableLayoutPanel.Margin.Right, 0);
			}
			selectDatabaseComboBox.AccessibleName = SqlConnectionUIControl.TextWithoutMnemonics(selectDatabaseRadioButton.Text);
			attachDatabaseTextBox.AccessibleName = SqlConnectionUIControl.TextWithoutMnemonics(attachDatabaseRadioButton.Text);
			_uiThread = Thread.CurrentThread;
		}

		public void Initialize(IDataConnectionProperties connectionProperties)
		{
			switch (connectionProperties)
			{
				case null:
					throw new ArgumentNullException(nameof(connectionProperties));
				case SqlConnectionProperties _:
				case OleDBSqlConnectionProperties _:
					if (connectionProperties is OleDBSqlConnectionProperties)
						currentOleDBProvider = connectionProperties["Provider"] as string;
					if (connectionProperties is OdbcConnectionProperties)
						savePasswordCheckBox.Enabled = false;
					_controlProperties = new SqlConnectionUIControl.ControlProperties(connectionProperties);
					break;
				default:
					throw new ArgumentException(SR.GetString("SqlConnectionUIControl_InvalidConnectionProperties"));
			}
		}

		public void LoadProperties()
		{
			_loading = true;
			if (currentOleDBProvider != Properties.Provider)
			{
				selectDatabaseComboBox.Items.Clear();
				currentOleDBProvider = Properties.Provider;
			}
			serverComboBox.Text = Properties.ServerName;
			if (Properties.UseWindowsAuthentication)
				windowsAuthenticationRadioButton.Checked = true;
			else
				sqlAuthenticationRadioButton.Checked = true;
			if (currentUserInstanceSetting != Properties.UserInstance)
				selectDatabaseComboBox.Items.Clear();
			currentUserInstanceSetting = Properties.UserInstance;
			userNameTextBox.Text = Properties.UserName;
			passwordTextBox.Text = Properties.Password;
			savePasswordCheckBox.Checked = Properties.SavePassword;
			if (Properties.DatabaseFile == null || Properties.DatabaseFile.Length == 0)
			{
				selectDatabaseRadioButton.Checked = true;
				selectDatabaseComboBox.Text = Properties.DatabaseName;
				attachDatabaseTextBox.Text = (string)null;
				logicalDatabaseNameTextBox.Text = (string)null;
			}
			else
			{
				attachDatabaseRadioButton.Checked = true;
				selectDatabaseComboBox.Text = (string)null;
				attachDatabaseTextBox.Text = Properties.DatabaseFile;
				logicalDatabaseNameTextBox.Text = Properties.LogicalDatabaseName;
			}
			_loading = false;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)serverLabel, (Control)serverTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)windowsAuthenticationRadioButton);
				LayoutUtils.MirrorControl((Control)sqlAuthenticationRadioButton);
				LayoutUtils.MirrorControl((Control)loginTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)selectDatabaseRadioButton);
				LayoutUtils.MirrorControl((Control)selectDatabaseComboBox);
				LayoutUtils.MirrorControl((Control)attachDatabaseRadioButton);
				LayoutUtils.MirrorControl((Control)attachDatabaseTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)logicalDatabaseNameLabel);
				LayoutUtils.MirrorControl((Control)logicalDatabaseNameTextBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)logicalDatabaseNameTextBox);
				LayoutUtils.UnmirrorControl((Control)logicalDatabaseNameLabel);
				LayoutUtils.UnmirrorControl((Control)attachDatabaseTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)attachDatabaseRadioButton);
				LayoutUtils.UnmirrorControl((Control)selectDatabaseComboBox);
				LayoutUtils.UnmirrorControl((Control)selectDatabaseRadioButton);
				LayoutUtils.UnmirrorControl((Control)loginTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)sqlAuthenticationRadioButton);
				LayoutUtils.UnmirrorControl((Control)windowsAuthenticationRadioButton);
				LayoutUtils.UnmirrorControl((Control)serverLabel, (Control)serverTableLayoutPanel);
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
			if (ActiveControl == selectDatabaseRadioButton && (keyData & Keys.KeyCode) == Keys.Down)
			{
				attachDatabaseRadioButton.Focus();
				return true;
			}
			if (ActiveControl != attachDatabaseRadioButton || (keyData & Keys.KeyCode) != Keys.Down)
				return base.ProcessDialogKey(keyData);
			selectDatabaseRadioButton.Focus();
			return true;
		}

		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			if (Parent != null)
				return;
			OnFontChanged(e);
		}

		private void HandleComboBoxDownKey(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Down)
				return;
			if (sender == serverComboBox)
				EnumerateServers(sender, (EventArgs)e);
			if (sender != selectDatabaseComboBox)
				return;
			EnumerateDatabases(sender, (EventArgs)e);
		}

		private void EnumerateServers(object sender, EventArgs e)
		{
			if (serverComboBox.Items.Count != 0)
				return;
			Cursor current = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				if (_serverEnumerationThread == null || _serverEnumerationThread.ThreadState == ThreadState.Stopped)
				{
					EnumerateServers();
				}
				else
				{
					if (_serverEnumerationThread.ThreadState != ThreadState.Running)
						return;
					_serverEnumerationThread.Join();
					PopulateServerComboBox();
				}
			}
			finally
			{
				Cursor.Current = current;
			}
		}

		private void SetServer(object sender, EventArgs e)
		{
			if (!_loading)
			{
				Properties.ServerName = serverComboBox.Text;
				if (serverComboBox.Items.Count == 0 && _serverEnumerationThread == null)
				{
					_serverEnumerationThread = new Thread(new ThreadStart(EnumerateServers));
					_serverEnumerationThread.Start();
				}
			}
			SetDatabaseGroupBoxStatus(sender, e);
			selectDatabaseComboBox.Items.Clear();
		}

		private void RefreshServers(object sender, EventArgs e)
		{
			serverComboBox.Items.Clear();
			EnumerateServers(sender, e);
		}

		private void SetAuthenticationOption(object sender, EventArgs e)
		{
			if (windowsAuthenticationRadioButton.Checked)
			{
				if (!_loading)
				{
					Properties.UseWindowsAuthentication = true;
					Properties.UserName = (string)null;
					Properties.Password = (string)null;
					Properties.SavePassword = false;
				}
				loginTableLayoutPanel.Enabled = false;
			}
			else
			{
				if (!_loading)
				{
					Properties.UseWindowsAuthentication = false;
					SetUserName(sender, e);
					SetPassword(sender, e);
					SetSavePassword(sender, e);
				}
				loginTableLayoutPanel.Enabled = true;
			}
			SetDatabaseGroupBoxStatus(sender, e);
			selectDatabaseComboBox.Items.Clear();
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (!_loading)
				Properties.UserName = userNameTextBox.Text;
			SetDatabaseGroupBoxStatus(sender, e);
			selectDatabaseComboBox.Items.Clear();
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (!_loading)
			{
				Properties.Password = passwordTextBox.Text;
				passwordTextBox.Text = passwordTextBox.Text;
			}
			selectDatabaseComboBox.Items.Clear();
		}

		private void SetSavePassword(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties.SavePassword = savePasswordCheckBox.Checked;
		}

		private void SetDatabaseGroupBoxStatus(object sender, EventArgs e)
		{
			if (serverComboBox.Text.Trim().Length > 0 && (windowsAuthenticationRadioButton.Checked || userNameTextBox.Text.Trim().Length > 0))
				databaseGroupBox.Enabled = true;
			else
				databaseGroupBox.Enabled = false;
		}

		private void SetDatabaseOption(object sender, EventArgs e)
		{
			if (selectDatabaseRadioButton.Checked)
			{
				SetDatabase(sender, e);
				SetAttachDatabase(sender, e);
				selectDatabaseComboBox.Enabled = true;
				attachDatabaseTableLayoutPanel.Enabled = false;
				logicalDatabaseNameLabel.Enabled = false;
				logicalDatabaseNameTextBox.Enabled = false;
			}
			else
			{
				SetAttachDatabase(sender, e);
				SetLogicalFilename(sender, e);
				selectDatabaseComboBox.Enabled = false;
				attachDatabaseTableLayoutPanel.Enabled = true;
				logicalDatabaseNameLabel.Enabled = true;
				logicalDatabaseNameTextBox.Enabled = true;
			}
		}

		private void SetDatabase(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties.DatabaseName = selectDatabaseComboBox.Text;
			if (selectDatabaseComboBox.Items.Count != 0 || _databaseEnumerationThread != null)
				return;
			_databaseEnumerationThread = new Thread(new ThreadStart(EnumerateDatabases));
			_databaseEnumerationThread.Start();
		}

		private void EnumerateDatabases(object sender, EventArgs e)
		{
			if (selectDatabaseComboBox.Items.Count != 0)
				return;
			Cursor current = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				if (_databaseEnumerationThread == null || _databaseEnumerationThread.ThreadState == ThreadState.Stopped)
				{
					EnumerateDatabases();
				}
				else
				{
					if (_databaseEnumerationThread.ThreadState != ThreadState.Running)
						return;
					_databaseEnumerationThread.Join();
					PopulateDatabaseComboBox();
				}
			}
			finally
			{
				Cursor.Current = current;
			}
		}

		private void SetAttachDatabase(object sender, EventArgs e)
		{
			if (_loading)
				return;
			if (selectDatabaseRadioButton.Checked)
				Properties.DatabaseFile = (string)null;
			else
				Properties.DatabaseFile = attachDatabaseTextBox.Text;
		}

		private void SetLogicalFilename(object sender, EventArgs e)
		{
			if (_loading)
				return;
			if (selectDatabaseRadioButton.Checked)
				Properties.LogicalDatabaseName = (string)null;
			else
				Properties.LogicalDatabaseName = logicalDatabaseNameTextBox.Text;
		}

		private void Browse(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = SR.GetString("SqlConnectionUIControl_BrowseFileTitle");
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			openFileDialog.Filter = SR.GetString("SqlConnectionUIControl_BrowseFileFilter");
			openFileDialog.DefaultExt = SR.GetString("SqlConnectionUIControl_BrowseFileDefaultExt");
			if (Container != null)
				Container.Add((IComponent)openFileDialog);
			try
			{
				if (openFileDialog.ShowDialog((IWin32Window)ParentForm) != DialogResult.OK)
					return;
				attachDatabaseTextBox.Text = openFileDialog.FileName.Trim();
			}
			finally
			{
				if (Container != null)
					Container.Remove((IComponent)openFileDialog);
				openFileDialog.Dispose();
			}
		}

		private void TrimControlText(object sender, EventArgs e)
		{
			Control control = sender as Control;
			control.Text = control.Text.Trim();
		}

		private void EnumerateServers()
		{
			DataTable dataTable;
			try
			{
#if NETFRAMEWORK
				dataTable = System.Data.Sql.SqlDataSourceEnumerator.Instance.GetDataSources();
#else
				dataTable = Microsoft.Data.Sql.SqlDataSourceEnumerator.Instance.GetDataSources();
#endif
			}
			catch
			{
				dataTable = new DataTable();
				dataTable.Locale = CultureInfo.InvariantCulture;
			}
			_servers = new object[dataTable.Rows.Count];
			for (int index = 0; index < _servers.Length; ++index)
			{
				string str1 = dataTable.Rows[index]["ServerName"].ToString();
				string str2 = dataTable.Rows[index]["InstanceName"].ToString();
				_servers[index] = str2.Length != 0 ? (object)(str1 + "\\" + str2) : (object)str1;
			}
			Array.Sort<object>(_servers);
			if (Thread.CurrentThread == _uiThread)
			{
				PopulateServerComboBox();
			}
			else
			{
				if (!IsHandleCreated)
					return;
				BeginInvoke((Delegate)new ThreadStart(PopulateServerComboBox));
			}
		}

		private void PopulateServerComboBox()
		{
			if (serverComboBox.Items.Count != 0)
				return;
			if (_servers.Length > 0)
				serverComboBox.Items.AddRange(_servers);
			else
				serverComboBox.Items.Add((object)string.Empty);
		}

		private void EnumerateDatabases()
		{
			DataTable dataTable = (DataTable)null;
			IDbConnection dbConnection = (IDbConnection)null;
			IDataReader reader = (IDataReader)null;
			try
			{
				dbConnection = Properties.GetBasicConnection();
				IDbCommand command = dbConnection.CreateCommand();
				command.CommandText = "SELECT CASE WHEN SERVERPROPERTY(N'EDITION') = 'SQL Data Services' OR SERVERPROPERTY(N'EDITION') = 'SQL Azure' THEN 1 ELSE 0 END";
				dbConnection.Open();
				command.CommandText = (int)command.ExecuteScalar() != 1 ? "SELECT name FROM master.dbo.sysdatabases WHERE HAS_DBACCESS(name) = 1 ORDER BY name" : "SELECT name FROM master.dbo.sysdatabases ORDER BY name";
				reader = command.ExecuteReader();
				dataTable = new DataTable();
				dataTable.Locale = CultureInfo.CurrentCulture;
				dataTable.Load(reader);
			}
			catch
			{
				dataTable = new DataTable();
				dataTable.Locale = CultureInfo.InvariantCulture;
			}
			finally
			{
				reader?.Dispose();
				dbConnection?.Dispose();
			}
			_databases = new object[dataTable.Rows.Count];
			for (int index = 0; index < _databases.Length; ++index)
				_databases[index] = dataTable.Rows[index]["name"];
			if (Thread.CurrentThread == _uiThread)
			{
				PopulateDatabaseComboBox();
			}
			else
			{
				if (!IsHandleCreated)
					return;
				BeginInvoke((Delegate)new ThreadStart(PopulateDatabaseComboBox));
			}
		}

		private void PopulateDatabaseComboBox()
		{
			if (selectDatabaseComboBox.Items.Count == 0)
			{
				if (_databases.Length > 0)
					selectDatabaseComboBox.Items.AddRange(_databases);
				else
					selectDatabaseComboBox.Items.Add((object)string.Empty);
			}
			_databaseEnumerationThread = (Thread)null;
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

		private SqlConnectionUIControl.ControlProperties Properties => _controlProperties;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
				components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(SqlConnectionUIControl));
			serverLabel = new Label();
			serverTableLayoutPanel = new TableLayoutPanel();
			serverComboBox = new ComboBox();
			refreshButton = new Button();
			logonGroupBox = new GroupBox();
			loginTableLayoutPanel = new TableLayoutPanel();
			userNameLabel = new Label();
			userNameTextBox = new TextBox();
			passwordLabel = new Label();
			passwordTextBox = new TextBox();
			savePasswordCheckBox = new CheckBox();
			sqlAuthenticationRadioButton = new RadioButton();
			windowsAuthenticationRadioButton = new RadioButton();
			databaseGroupBox = new GroupBox();
			logicalDatabaseNameTextBox = new TextBox();
			logicalDatabaseNameLabel = new Label();
			attachDatabaseTableLayoutPanel = new TableLayoutPanel();
			attachDatabaseTextBox = new TextBox();
			browseButton = new Button();
			attachDatabaseRadioButton = new RadioButton();
			selectDatabaseComboBox = new ComboBox();
			selectDatabaseRadioButton = new RadioButton();
			serverTableLayoutPanel.SuspendLayout();
			logonGroupBox.SuspendLayout();
			loginTableLayoutPanel.SuspendLayout();
			databaseGroupBox.SuspendLayout();
			attachDatabaseTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)serverLabel, "serverLabel");
			serverLabel.FlatStyle = FlatStyle.System;
			serverLabel.Name = "serverLabel";
			componentResourceManager.ApplyResources((object)serverTableLayoutPanel, "serverTableLayoutPanel");
			serverTableLayoutPanel.Controls.Add((Control)serverComboBox, 0, 0);
			serverTableLayoutPanel.Controls.Add((Control)refreshButton, 1, 0);
			serverTableLayoutPanel.Name = "serverTableLayoutPanel";
			componentResourceManager.ApplyResources((object)serverComboBox, "serverComboBox");
			serverComboBox.AutoCompleteMode = AutoCompleteMode.Append;
			serverComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
			serverComboBox.FormattingEnabled = true;
			serverComboBox.Name = "serverComboBox";
			serverComboBox.Leave += new EventHandler(TrimControlText);
			serverComboBox.TextChanged += new EventHandler(SetServer);
			serverComboBox.KeyDown += new KeyEventHandler(HandleComboBoxDownKey);
			serverComboBox.DropDown += new EventHandler(EnumerateServers);
			componentResourceManager.ApplyResources((object)refreshButton, "refreshButton");
			refreshButton.MinimumSize = new Size(75, 23);
			refreshButton.Name = "refreshButton";
			refreshButton.Click += new EventHandler(RefreshServers);
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
			componentResourceManager.ApplyResources((object)databaseGroupBox, "databaseGroupBox");
			databaseGroupBox.Controls.Add((Control)logicalDatabaseNameTextBox);
			databaseGroupBox.Controls.Add((Control)logicalDatabaseNameLabel);
			databaseGroupBox.Controls.Add((Control)attachDatabaseTableLayoutPanel);
			databaseGroupBox.Controls.Add((Control)attachDatabaseRadioButton);
			databaseGroupBox.Controls.Add((Control)selectDatabaseComboBox);
			databaseGroupBox.Controls.Add((Control)selectDatabaseRadioButton);
			databaseGroupBox.FlatStyle = FlatStyle.System;
			databaseGroupBox.Name = "databaseGroupBox";
			databaseGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)logicalDatabaseNameTextBox, "logicalDatabaseNameTextBox");
			logicalDatabaseNameTextBox.Name = "logicalDatabaseNameTextBox";
			logicalDatabaseNameTextBox.Leave += new EventHandler(TrimControlText);
			logicalDatabaseNameTextBox.TextChanged += new EventHandler(SetLogicalFilename);
			componentResourceManager.ApplyResources((object)logicalDatabaseNameLabel, "logicalDatabaseNameLabel");
			logicalDatabaseNameLabel.FlatStyle = FlatStyle.System;
			logicalDatabaseNameLabel.Name = "logicalDatabaseNameLabel";
			componentResourceManager.ApplyResources((object)attachDatabaseTableLayoutPanel, "attachDatabaseTableLayoutPanel");
			attachDatabaseTableLayoutPanel.Controls.Add((Control)attachDatabaseTextBox, 0, 0);
			attachDatabaseTableLayoutPanel.Controls.Add((Control)browseButton, 1, 0);
			attachDatabaseTableLayoutPanel.Name = "attachDatabaseTableLayoutPanel";
			componentResourceManager.ApplyResources((object)attachDatabaseTextBox, "attachDatabaseTextBox");
			attachDatabaseTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
			attachDatabaseTextBox.AutoCompleteSource = AutoCompleteSource.FileSystem;
			attachDatabaseTextBox.Name = "attachDatabaseTextBox";
			attachDatabaseTextBox.Leave += new EventHandler(TrimControlText);
			attachDatabaseTextBox.TextChanged += new EventHandler(SetAttachDatabase);
			componentResourceManager.ApplyResources((object)browseButton, "browseButton");
			browseButton.MinimumSize = new Size(75, 23);
			browseButton.Name = "browseButton";
			browseButton.Click += new EventHandler(Browse);
			componentResourceManager.ApplyResources((object)attachDatabaseRadioButton, "attachDatabaseRadioButton");
			attachDatabaseRadioButton.Name = "attachDatabaseRadioButton";
			attachDatabaseRadioButton.CheckedChanged += new EventHandler(SetDatabaseOption);
			componentResourceManager.ApplyResources((object)selectDatabaseComboBox, "selectDatabaseComboBox");
			selectDatabaseComboBox.AutoCompleteMode = AutoCompleteMode.Append;
			selectDatabaseComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
			selectDatabaseComboBox.FormattingEnabled = true;
			selectDatabaseComboBox.Name = "selectDatabaseComboBox";
			selectDatabaseComboBox.Leave += new EventHandler(TrimControlText);
			selectDatabaseComboBox.TextChanged += new EventHandler(SetDatabase);
			selectDatabaseComboBox.KeyDown += new KeyEventHandler(HandleComboBoxDownKey);
			selectDatabaseComboBox.DropDown += new EventHandler(EnumerateDatabases);
			componentResourceManager.ApplyResources((object)selectDatabaseRadioButton, "selectDatabaseRadioButton");
			selectDatabaseRadioButton.Name = "selectDatabaseRadioButton";
			selectDatabaseRadioButton.CheckedChanged += new EventHandler(SetDatabaseOption);
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add((Control)databaseGroupBox);
			Controls.Add((Control)logonGroupBox);
			Controls.Add((Control)serverTableLayoutPanel);
			Controls.Add((Control)serverLabel);
			MinimumSize = new Size(350, 360);
			Name = nameof(SqlConnectionUIControl);
			serverTableLayoutPanel.ResumeLayout(false);
			serverTableLayoutPanel.PerformLayout();
			logonGroupBox.ResumeLayout(false);
			logonGroupBox.PerformLayout();
			loginTableLayoutPanel.ResumeLayout(false);
			loginTableLayoutPanel.PerformLayout();
			databaseGroupBox.ResumeLayout(false);
			databaseGroupBox.PerformLayout();
			attachDatabaseTableLayoutPanel.ResumeLayout(false);
			attachDatabaseTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}

		private class ControlProperties
		{
			private IDataConnectionProperties _properties;

			public ControlProperties(IDataConnectionProperties properties)
			{
				_properties = properties;
			}

			public string Provider
			{
				get
				{
					return _properties is OleDBSqlConnectionProperties ? _properties[nameof(Provider)] as string : (string)null;
				}
			}

			public string ServerName
			{
				get => _properties[ServerNameProperty] as string;
				set
				{
					if (value != null && value.Trim().Length > 0)
						_properties[ServerNameProperty] = (object)value.Trim();
					else
						_properties.Reset(ServerNameProperty);
				}
			}

			public bool UserInstance
			{
				get
				{
					return _properties is SqlConnectionProperties && (bool)_properties["User Instance"];
				}
			}

			public bool UseWindowsAuthentication
			{
				get
				{
					if (_properties is SqlConnectionProperties)
						return (bool)_properties["Integrated Security"];
					return _properties is OleDBConnectionProperties ? _properties.Contains("Integrated Security") && _properties["Integrated Security"] is string && (_properties["Integrated Security"] as string).Equals("SSPI", StringComparison.OrdinalIgnoreCase) : _properties is OdbcConnectionProperties && _properties.Contains("Trusted_Connection") && _properties["Trusted_Connection"] is string && (_properties["Trusted_Connection"] as string).Equals("Yes", StringComparison.OrdinalIgnoreCase);
				}
				set
				{
					if (_properties is SqlConnectionProperties)
					{
						if (value)
							_properties["Integrated Security"] = (object)value;
						else
							_properties.Reset("Integrated Security");
					}
					if (_properties is OleDBConnectionProperties)
					{
						if (value)
							_properties["Integrated Security"] = (object)"SSPI";
						else
							_properties.Reset("Integrated Security");
					}
					if (!(_properties is OdbcConnectionProperties))
						return;
					if (value)
						_properties["Trusted_Connection"] = (object)"Yes";
					else
						_properties.Remove("Trusted_Connection");
				}
			}

			public string UserName
			{
				get => _properties[UserNameProperty] as string;
				set
				{
					if (value != null && value.Trim().Length > 0)
						_properties[UserNameProperty] = (object)value.Trim();
					else
						_properties.Reset(UserNameProperty);
				}
			}

			public string Password
			{
				get => _properties[PasswordProperty] as string;
				set
				{
					if (value != null && value.Length > 0)
						_properties[PasswordProperty] = (object)value;
					else
						_properties.Reset(PasswordProperty);
				}
			}

			public string GetPassword()
			{
				return Password;
			}

			public bool SavePassword
			{
				get
				{
					return !(_properties is OdbcConnectionProperties) && (bool)_properties["Persist Security Info"];
				}
				set
				{
					if (value)
						_properties["Persist Security Info"] = (object)value;
					else
						_properties.Reset("Persist Security Info");
				}
			}

			public string DatabaseName
			{
				get => _properties[DatabaseNameProperty] as string;
				set
				{
					if (value != null && value.Trim().Length > 0)
						_properties[DatabaseNameProperty] = (object)value.Trim();
					else
						_properties.Reset(DatabaseNameProperty);
				}
			}

			public string DatabaseFile
			{
				get => _properties[DatabaseFileProperty] as string;
				set
				{
					if (value != null && value.Trim().Length > 0)
						_properties[DatabaseFileProperty] = (object)value.Trim();
					else
						_properties.Reset(DatabaseFileProperty);
				}
			}

			public string LogicalDatabaseName
			{
				get => DatabaseName;
				set => DatabaseName = value;
			}

			public IDbConnection GetBasicConnection()
			{
				IDbConnection basicConnection = (IDbConnection)null;
				string connectionString = string.Empty;
				if (_properties is SqlConnectionProperties || _properties is OleDBConnectionProperties)
				{
					if (_properties is OleDBConnectionProperties)
						connectionString = connectionString + "Provider=" + _properties["Provider"].ToString() + ";";
					string str = connectionString + "Data Source='" + ServerName.Replace("'", "''") + "';";
					if (UserInstance)
						str += "User Instance=true;";
					connectionString = !UseWindowsAuthentication ? str + "User ID='" + UserName.Replace("'", "''") + "';" + "Password='" + GetPassword().Replace("'", "''") + "';" : str + "Integrated Security=" + _properties["Integrated Security"].ToString() + ";";
					if (_properties is SqlConnectionProperties)
						connectionString += "Pooling=False;";
				}
				if (_properties is OdbcConnectionProperties)
				{
					string str = connectionString + "DRIVER={SQL Server};" + "SERVER={" + ServerName.Replace("}", "}}") + "};";
					connectionString = !UseWindowsAuthentication ? str + "UID={" + UserName.Replace("}", "}}") + "};" + "PWD={" + GetPassword().Replace("}", "}}") + "};" : str + "Trusted_Connection=Yes;";
				}
				if (_properties is SqlConnectionProperties)
					basicConnection = (IDbConnection)new SqlConnection(connectionString);
				if (_properties is OleDBConnectionProperties)
					basicConnection = (IDbConnection)new OleDbConnection(connectionString);
				if (_properties is OdbcConnectionProperties)
					basicConnection = (IDbConnection)new OdbcConnection(connectionString);
				return basicConnection;
			}

			private string ServerNameProperty
			{
				get
				{
					if (_properties is SqlConnectionProperties || _properties is OleDBConnectionProperties)
						return "Data Source";
					return !(_properties is OdbcConnectionProperties) ? (string)null : "SERVER";
				}
			}

			private string UserNameProperty
			{
				get
				{
					if (_properties is SqlConnectionProperties || _properties is OleDBConnectionProperties)
						return "User ID";
					return !(_properties is OdbcConnectionProperties) ? (string)null : "UID";
				}
			}

			private string PasswordProperty
			{
				get
				{
					if (_properties is SqlConnectionProperties || _properties is OleDBConnectionProperties)
						return "Password";
					return !(_properties is OdbcConnectionProperties) ? (string)null : "PWD";
				}
			}

			private string DatabaseNameProperty
			{
				get
				{
					if (_properties is SqlConnectionProperties || _properties is OleDBConnectionProperties)
						return "Initial Catalog";
					return !(_properties is OdbcConnectionProperties) ? (string)null : "DATABASE";
				}
			}

			private string DatabaseFileProperty
			{
				get
				{
					if (_properties is SqlConnectionProperties)
						return "AttachDbFilename";
					if (_properties is OleDBConnectionProperties)
						return "Initial File Name";
					return !(_properties is OdbcConnectionProperties) ? (string)null : "AttachDBFileName";
				}
			}
		}
	}
}
