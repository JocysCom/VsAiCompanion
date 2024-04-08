using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public class OleDBConnectionUIControl : UserControl, IDataConnectionUIControl
	{
		private bool _loading;
		private object[] _catalogs;
		private Thread _uiThread;
		private Thread _catalogEnumerationThread;
		private IDataConnectionProperties _connectionProperties;
		private IContainer components;
		private Label providerLabel;
		private TableLayoutPanel providerTableLayoutPanel;
		private ComboBox providerComboBox;
		private Button dataLinksButton;
		private GroupBox dataSourceGroupBox;
		private TableLayoutPanel dataSourceTableLayoutPanel;
		private Label dataSourceLabel;
		private TextBox dataSourceTextBox;
		private Label locationLabel;
		private TextBox locationTextBox;
		private GroupBox logonGroupBox;
		private RadioButton integratedSecurityRadioButton;
		private RadioButton nativeSecurityRadioButton;
		private TableLayoutPanel loginTableLayoutPanel;
		private TableLayoutPanel subLoginTableLayoutPanel;
		private Label userNameLabel;
		private TextBox userNameTextBox;
		private Label passwordLabel;
		private TextBox passwordTextBox;
		private TableLayoutPanel subSubLoginTableLayoutPanel;
		private CheckBox blankPasswordCheckBox;
		private CheckBox allowSavingPasswordCheckBox;
		private Label initialCatalogLabel;
		private ComboBox initialCatalogComboBox;

		public OleDBConnectionUIControl()
		{
			InitializeComponent();
			RightToLeft = RightToLeft.Inherit;
			_uiThread = Thread.CurrentThread;
		}

		public void Initialize(IDataConnectionProperties connectionProperties)
		{
			Initialize(connectionProperties, false);
		}

		public void Initialize(
		  IDataConnectionProperties connectionProperties,
		  bool disableProviderSelection)
		{
			if (!(connectionProperties is OleDBConnectionProperties))
				throw new ArgumentException(SR.GetString("OleDBConnectionUIControl_InvalidConnectionProperties"));
			EnumerateProviders();
			providerComboBox.Enabled = !disableProviderSelection;
			dataLinksButton.Enabled = false;
			dataSourceGroupBox.Enabled = false;
			logonGroupBox.Enabled = false;
			initialCatalogLabel.Enabled = false;
			initialCatalogComboBox.Enabled = false;
			_connectionProperties = connectionProperties;
		}

		public void LoadProperties()
		{
			_loading = true;
			if (Properties["Provider"] is string property && property.Length > 0)
			{
				object obj = (object)null;
				foreach (OleDBConnectionUIControl.ProviderStruct providerStruct in providerComboBox.Items)
				{
					if (providerStruct.ProgId.Equals(property))
					{
						obj = (object)providerStruct;
						break;
					}
					if (providerStruct.ProgId.StartsWith(property + ".", StringComparison.OrdinalIgnoreCase) && (obj == null || providerStruct.ProgId.CompareTo(((OleDBConnectionUIControl.ProviderStruct)obj).ProgId) > 0))
						obj = (object)providerStruct;
				}
				providerComboBox.SelectedItem = obj;
			}
			else
				providerComboBox.SelectedItem = (object)null;
			if (Properties.Contains("Data Source") && Properties["Data Source"] is string)
				dataSourceTextBox.Text = Properties["Data Source"] as string;
			else
				dataSourceTextBox.Text = (string)null;
			if (Properties.Contains("Location") && Properties["Location"] is string)
				locationTextBox.Text = Properties["Location"] as string;
			else
				locationTextBox.Text = (string)null;
			if (Properties.Contains("Integrated Security") && Properties["Integrated Security"] is string && (Properties["Integrated Security"] as string).Length > 0)
				integratedSecurityRadioButton.Checked = true;
			else
				nativeSecurityRadioButton.Checked = true;
			if (Properties.Contains("User ID") && Properties["User ID"] is string)
				userNameTextBox.Text = Properties["User ID"] as string;
			else
				userNameTextBox.Text = (string)null;
			if (Properties.Contains("Password") && Properties["Password"] is string)
			{
				passwordTextBox.Text = Properties["Password"] as string;
				blankPasswordCheckBox.Checked = passwordTextBox.Text.Length == 0;
			}
			else
			{
				passwordTextBox.Text = (string)null;
				blankPasswordCheckBox.Checked = false;
			}
			allowSavingPasswordCheckBox.Checked = Properties.Contains("Persist Security Info") && Properties["Persist Security Info"] is bool && (bool)Properties["Persist Security Info"];
			if (Properties.Contains("Initial Catalog") && Properties["Initial Catalog"] is string)
				initialCatalogComboBox.Text = Properties["Initial Catalog"] as string;
			else
				initialCatalogComboBox.Text = (string)null;
			_loading = false;
		}

		public override Size GetPreferredSize(Size proposedSize)
		{
			Size preferredSize = base.GetPreferredSize(proposedSize);
			int width = logonGroupBox.Padding.Left + loginTableLayoutPanel.Margin.Left + blankPasswordCheckBox.Margin.Left + blankPasswordCheckBox.Width + blankPasswordCheckBox.Margin.Right + allowSavingPasswordCheckBox.Margin.Left + allowSavingPasswordCheckBox.Width + allowSavingPasswordCheckBox.Margin.Right + loginTableLayoutPanel.Margin.Right + logonGroupBox.Padding.Right;
			if (width > preferredSize.Width)
				preferredSize = new Size(width, preferredSize.Height);
			return preferredSize;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (ParentForm != null && ParentForm.RightToLeftLayout && RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)providerLabel, (Control)providerTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)integratedSecurityRadioButton);
				LayoutUtils.MirrorControl((Control)nativeSecurityRadioButton);
				LayoutUtils.MirrorControl((Control)loginTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)initialCatalogLabel, (Control)initialCatalogComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)initialCatalogLabel, (Control)initialCatalogComboBox);
				LayoutUtils.UnmirrorControl((Control)loginTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)nativeSecurityRadioButton);
				LayoutUtils.UnmirrorControl((Control)integratedSecurityRadioButton);
				LayoutUtils.UnmirrorControl((Control)providerLabel, (Control)providerTableLayoutPanel);
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

		private void EnumerateProviders()
		{
			Cursor current = Cursor.Current;
			OleDbDataReader oleDbDataReader = (OleDbDataReader)null;
			try
			{
				Cursor.Current = Cursors.WaitCursor;
				oleDbDataReader = OleDbEnumerator.GetEnumerator(Type.GetTypeFromCLSID(NativeMethods.CLSID_OLEDB_ENUMERATOR));
				Dictionary<string, string> dictionary1 = new Dictionary<string, string>();
				while (oleDbDataReader.Read())
				{
					switch (oleDbDataReader.GetInt32(oleDbDataReader.GetOrdinal("SOURCES_TYPE")))
					{
						case 1:
						case 3:
							string key1 = oleDbDataReader["SOURCES_CLSID"].ToString();
							string str = oleDbDataReader["SOURCES_DESCRIPTION"].ToString();
							dictionary1[key1] = str;
							continue;
						default:
							continue;
					}
				}
				Dictionary<string, string> dictionary2 = new Dictionary<string, string>(dictionary1.Count);
				RegistryKey registryKey1 = Registry.ClassesRoot.OpenSubKey("CLSID");
				using (registryKey1)
				{
					foreach (KeyValuePair<string, string> keyValuePair in dictionary1)
					{
						RegistryKey registryKey2 = registryKey1.OpenSubKey(keyValuePair.Key + "\\ProgID");
						if (registryKey2 != null)
						{
							using (registryKey2)
							{
								if (registryKey1.OpenSubKey(keyValuePair.Key + "\\ProgID").GetValue((string)null) is string key2)
								{
									if (!key2.Equals("MSDASQL", StringComparison.OrdinalIgnoreCase))
									{
										if (!key2.StartsWith("MSDASQL.", StringComparison.OrdinalIgnoreCase))
										{
											if (!key2.Equals("Microsoft OLE DB Provider for ODBC Drivers"))
												dictionary2[key2] = keyValuePair.Key;
										}
									}
								}
							}
						}
					}
				}
				foreach (KeyValuePair<string, string> keyValuePair in dictionary2)
					providerComboBox.Items.Add((object)new OleDBConnectionUIControl.ProviderStruct(keyValuePair.Key, dictionary1[keyValuePair.Value]));
			}
			finally
			{
				oleDbDataReader?.Dispose();
				Cursor.Current = current;
			}
		}

		private void SetProvider(object sender, EventArgs e)
		{
			if (providerComboBox.SelectedItem is OleDBConnectionUIControl.ProviderStruct)
			{
				if (!_loading)
					Properties["Provider"] = (object)((OleDBConnectionUIControl.ProviderStruct)providerComboBox.SelectedItem).ProgId;
				foreach (PropertyDescriptor property in TypeDescriptor.GetProperties((object)Properties))
				{
					if (property.Category.Equals(CategoryAttribute.Default.Category, StringComparison.CurrentCulture))
						Properties.Remove(property.DisplayName);
				}
				dataLinksButton.Enabled = true;
				dataSourceGroupBox.Enabled = true;
				logonGroupBox.Enabled = true;
				loginTableLayoutPanel.Enabled = true;
				initialCatalogLabel.Enabled = true;
				initialCatalogComboBox.Enabled = true;
				dataSourceLabel.Enabled = false;
				dataSourceTextBox.Enabled = false;
				locationLabel.Enabled = false;
				locationTextBox.Enabled = false;
				integratedSecurityRadioButton.Enabled = false;
				nativeSecurityRadioButton.Enabled = false;
				userNameLabel.Enabled = false;
				userNameTextBox.Enabled = false;
				passwordLabel.Enabled = false;
				passwordTextBox.Enabled = false;
				blankPasswordCheckBox.Enabled = false;
				allowSavingPasswordCheckBox.Enabled = false;
				initialCatalogLabel.Enabled = false;
				initialCatalogComboBox.Enabled = false;
				PropertyDescriptorCollection properties = TypeDescriptor.GetProperties((object)Properties);
				PropertyDescriptor propertyDescriptor1;
				if ((propertyDescriptor1 = properties["DataSource"]) != null && propertyDescriptor1.IsBrowsable)
				{
					dataSourceLabel.Enabled = true;
					dataSourceTextBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor2;
				if ((propertyDescriptor2 = properties["Location"]) != null && propertyDescriptor2.IsBrowsable)
				{
					locationLabel.Enabled = true;
					locationTextBox.Enabled = true;
				}
				dataSourceGroupBox.Enabled = dataSourceTextBox.Enabled || locationTextBox.Enabled;
				PropertyDescriptor propertyDescriptor3;
				if ((propertyDescriptor3 = properties["Integrated Security"]) != null && propertyDescriptor3.IsBrowsable)
					integratedSecurityRadioButton.Enabled = true;
				PropertyDescriptor propertyDescriptor4;
				if ((propertyDescriptor4 = properties["User ID"]) != null && propertyDescriptor4.IsBrowsable)
				{
					userNameLabel.Enabled = true;
					userNameTextBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor5;
				if ((propertyDescriptor5 = properties["Password"]) != null && propertyDescriptor5.IsBrowsable)
				{
					passwordLabel.Enabled = true;
					passwordTextBox.Enabled = true;
					blankPasswordCheckBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor6;
				if (passwordTextBox.Enabled && (propertyDescriptor6 = properties["PersistSecurityInfo"]) != null && propertyDescriptor6.IsBrowsable)
					allowSavingPasswordCheckBox.Enabled = true;
				loginTableLayoutPanel.Enabled = userNameTextBox.Enabled || passwordTextBox.Enabled;
				nativeSecurityRadioButton.Enabled = loginTableLayoutPanel.Enabled;
				logonGroupBox.Enabled = integratedSecurityRadioButton.Enabled || nativeSecurityRadioButton.Enabled;
				PropertyDescriptor propertyDescriptor7;
				if ((propertyDescriptor7 = properties["Initial Catalog"]) != null && propertyDescriptor7.IsBrowsable)
				{
					initialCatalogLabel.Enabled = true;
					initialCatalogComboBox.Enabled = true;
				}
			}
			else
			{
				if (!_loading)
					Properties["Provider"] = (object)null;
				dataLinksButton.Enabled = false;
				dataSourceGroupBox.Enabled = false;
				logonGroupBox.Enabled = false;
				initialCatalogLabel.Enabled = false;
				initialCatalogComboBox.Enabled = false;
			}
			if (!_loading)
				LoadProperties();
			initialCatalogComboBox.Items.Clear();
		}

		private void SetProviderDropDownWidth(object sender, EventArgs e)
		{
			if (providerComboBox.Items.Count > 0)
			{
				int num = 0;
				using (Graphics dc = Graphics.FromHwnd(providerComboBox.Handle))
				{
					foreach (OleDBConnectionUIControl.ProviderStruct providerStruct in providerComboBox.Items)
					{
						int width = TextRenderer.MeasureText((IDeviceContext)dc, providerStruct.Description, providerComboBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak).Width;
						if (width > num)
							num = width;
					}
				}
				providerComboBox.DropDownWidth = num + 3;
				if (providerComboBox.Items.Count <= providerComboBox.MaxDropDownItems)
					return;
				providerComboBox.DropDownWidth += SystemInformation.VerticalScrollBarWidth;
			}
			else
				providerComboBox.DropDownWidth = providerComboBox.Width;
		}

		private void ShowDataLinks(object sender, EventArgs e)
		{
			try
			{
				NativeMethods.IDataInitialize instance = Activator.CreateInstance(Type.GetTypeFromCLSID(NativeMethods.CLSID_DataLinks)) as NativeMethods.IDataInitialize;
				object ppDataSource = (object)null;
				instance.GetDataSource((object)null, 1, Properties.ToFullString(), ref NativeMethods.IID_IUnknown, ref ppDataSource);
				((NativeMethods.IDBPromptInitialize)instance).PromptDataSource((object)null, ParentForm.Handle, 18, 0, IntPtr.Zero, (string)null, ref NativeMethods.IID_IUnknown, ref ppDataSource);
				string ppwszInitString = (string)null;
				instance.GetInitializationString(ppDataSource, true, out ppwszInitString);
				Properties.Parse(ppwszInitString);
				LoadProperties();
			}
			catch (Exception ex)
			{
				if (ex is COMException comException && comException.ErrorCode == -2147217842)
					return;
				if (GetService(typeof(IUIService)) is IUIService service)
				{
					service.ShowError(ex);
				}
				else
				{
					int num = (int)RTLAwareMessageBox.Show((string)null, ex.Message, MessageBoxIcon.Exclamation);
				}
			}
		}

		private void SetDataSource(object sender, EventArgs e)
		{
			if (!_loading)
				Properties["Data Source"] = dataSourceTextBox.Text.Trim().Length > 0 ? (object)dataSourceTextBox.Text.Trim() : (object)(string)null;
			initialCatalogComboBox.Items.Clear();
		}

		private void SetLocation(object sender, EventArgs e)
		{
			if (!_loading)
				Properties["Location"] = (object)locationTextBox.Text;
			initialCatalogComboBox.Items.Clear();
		}

		private void SetSecurityOption(object sender, EventArgs e)
		{
			if (!_loading)
			{
				if (integratedSecurityRadioButton.Checked)
				{
					Properties["Integrated Security"] = (object)"SSPI";
					Properties.Reset("User ID");
					Properties.Reset("Password");
					Properties.Reset("Persist Security Info");
				}
				else
				{
					Properties.Reset("Integrated Security");
					SetUserName(sender, e);
					SetPassword(sender, e);
					SetBlankPassword(sender, e);
					SetAllowSavingPassword(sender, e);
				}
			}
			loginTableLayoutPanel.Enabled = !integratedSecurityRadioButton.Checked;
			initialCatalogComboBox.Items.Clear();
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (!_loading)
				Properties["User ID"] = userNameTextBox.Text.Trim().Length > 0 ? (object)userNameTextBox.Text.Trim() : (object)(string)null;
			initialCatalogComboBox.Items.Clear();
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (!_loading)
			{
				Properties["Password"] = passwordTextBox.Text.Length > 0 ? (object)passwordTextBox.Text : (object)(string)null;
				if (passwordTextBox.Text.Length == 0)
					Properties.Remove("Password");
				passwordTextBox.Text = passwordTextBox.Text;
			}
			initialCatalogComboBox.Items.Clear();
		}

		private void SetBlankPassword(object sender, EventArgs e)
		{
			if (blankPasswordCheckBox.Checked)
			{
				if (!_loading)
					Properties["Password"] = (object)string.Empty;
				passwordLabel.Enabled = false;
				passwordTextBox.Enabled = false;
			}
			else
			{
				if (!_loading)
					SetPassword(sender, e);
				passwordLabel.Enabled = true;
				passwordTextBox.Enabled = true;
			}
		}

		private void SetAllowSavingPassword(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["Persist Security Info"] = (object)allowSavingPasswordCheckBox.Checked;
		}

		private void HandleComboBoxDownKey(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Down)
				return;
			EnumerateCatalogs(sender, (EventArgs)e);
		}

		private void SetInitialCatalog(object sender, EventArgs e)
		{
			if (_loading)
				return;
			Properties["Initial Catalog"] = initialCatalogComboBox.Text.Trim().Length > 0 ? (object)initialCatalogComboBox.Text.Trim() : (object)(string)null;
			if (initialCatalogComboBox.Items.Count != 0 || _catalogEnumerationThread != null)
				return;
			_catalogEnumerationThread = new Thread(new ThreadStart(EnumerateCatalogs));
			_catalogEnumerationThread.Start();
		}

		private void EnumerateCatalogs(object sender, EventArgs e)
		{
			if (initialCatalogComboBox.Items.Count != 0)
				return;
			Cursor current = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				if (_catalogEnumerationThread == null || _catalogEnumerationThread.ThreadState == ThreadState.Stopped)
				{
					EnumerateCatalogs();
				}
				else
				{
					if (_catalogEnumerationThread.ThreadState != ThreadState.Running)
						return;
					_catalogEnumerationThread.Join();
					PopulateInitialCatalogComboBox();
				}
			}
			finally
			{
				Cursor.Current = current;
			}
		}

		private void TrimControlText(object sender, EventArgs e)
		{
			Control control = sender as Control;
			control.Text = control.Text.Trim();
		}

		private void EnumerateCatalogs()
		{
			DataTable dataTable = (DataTable)null;
			OleDbConnection oleDbConnection = (OleDbConnection)null;
			try
			{
				OleDbConnectionStringBuilder connectionStringBuilder = new OleDbConnectionStringBuilder(Properties.ToFullString());
				connectionStringBuilder.Remove("Initial Catalog");
				oleDbConnection = new OleDbConnection(connectionStringBuilder.ConnectionString);
				oleDbConnection.Open();
				dataTable = oleDbConnection.GetOleDbSchemaTable(OleDbSchemaGuid.Catalogs, (object[])null);
			}
			catch
			{
				dataTable = new DataTable();
				dataTable.Locale = CultureInfo.InvariantCulture;
			}
			finally
			{
				oleDbConnection?.Dispose();
			}
			_catalogs = new object[dataTable.Rows.Count];
			for (int index = 0; index < _catalogs.Length; ++index)
				_catalogs[index] = dataTable.Rows[index]["CATALOG_NAME"];
			if (Thread.CurrentThread == _uiThread)
			{
				PopulateInitialCatalogComboBox();
			}
			else
			{
				if (!IsHandleCreated)
					return;
				BeginInvoke((Delegate)new ThreadStart(PopulateInitialCatalogComboBox));
			}
		}

		private void PopulateInitialCatalogComboBox()
		{
			if (initialCatalogComboBox.Items.Count == 0)
			{
				if (_catalogs.Length > 0)
					initialCatalogComboBox.Items.AddRange(_catalogs);
				else
					initialCatalogComboBox.Items.Add((object)string.Empty);
			}
			_catalogEnumerationThread = (Thread)null;
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
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(OleDBConnectionUIControl));
			providerLabel = new Label();
			providerTableLayoutPanel = new TableLayoutPanel();
			providerComboBox = new ComboBox();
			dataLinksButton = new Button();
			dataSourceGroupBox = new GroupBox();
			dataSourceTableLayoutPanel = new TableLayoutPanel();
			dataSourceLabel = new Label();
			dataSourceTextBox = new TextBox();
			locationLabel = new Label();
			locationTextBox = new TextBox();
			logonGroupBox = new GroupBox();
			loginTableLayoutPanel = new TableLayoutPanel();
			subLoginTableLayoutPanel = new TableLayoutPanel();
			userNameLabel = new Label();
			userNameTextBox = new TextBox();
			passwordLabel = new Label();
			passwordTextBox = new TextBox();
			subSubLoginTableLayoutPanel = new TableLayoutPanel();
			blankPasswordCheckBox = new CheckBox();
			allowSavingPasswordCheckBox = new CheckBox();
			nativeSecurityRadioButton = new RadioButton();
			integratedSecurityRadioButton = new RadioButton();
			initialCatalogLabel = new Label();
			initialCatalogComboBox = new ComboBox();
			providerTableLayoutPanel.SuspendLayout();
			dataSourceGroupBox.SuspendLayout();
			dataSourceTableLayoutPanel.SuspendLayout();
			logonGroupBox.SuspendLayout();
			loginTableLayoutPanel.SuspendLayout();
			subLoginTableLayoutPanel.SuspendLayout();
			subSubLoginTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)providerLabel, "providerLabel");
			providerLabel.FlatStyle = FlatStyle.System;
			providerLabel.Name = "providerLabel";
			componentResourceManager.ApplyResources((object)providerTableLayoutPanel, "providerTableLayoutPanel");
			providerTableLayoutPanel.Controls.Add((Control)providerComboBox, 0, 0);
			providerTableLayoutPanel.Controls.Add((Control)dataLinksButton, 1, -1);
			providerTableLayoutPanel.Name = "providerTableLayoutPanel";
			componentResourceManager.ApplyResources((object)providerComboBox, "providerComboBox");
			providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			providerComboBox.FormattingEnabled = true;
			providerComboBox.Name = "providerComboBox";
			providerComboBox.Sorted = true;
			providerComboBox.SelectedIndexChanged += new EventHandler(SetProvider);
			providerComboBox.DropDown += new EventHandler(SetProviderDropDownWidth);
			componentResourceManager.ApplyResources((object)dataLinksButton, "dataLinksButton");
			dataLinksButton.MinimumSize = new Size(83, 23);
			dataLinksButton.Name = "dataLinksButton";
			dataLinksButton.Click += new EventHandler(ShowDataLinks);
			componentResourceManager.ApplyResources((object)dataSourceGroupBox, "dataSourceGroupBox");
			dataSourceGroupBox.Controls.Add((Control)dataSourceTableLayoutPanel);
			dataSourceGroupBox.FlatStyle = FlatStyle.System;
			dataSourceGroupBox.Name = "dataSourceGroupBox";
			dataSourceGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)dataSourceTableLayoutPanel, "dataSourceTableLayoutPanel");
			dataSourceTableLayoutPanel.Controls.Add((Control)dataSourceLabel, 0, 0);
			dataSourceTableLayoutPanel.Controls.Add((Control)dataSourceTextBox, 1, 0);
			dataSourceTableLayoutPanel.Controls.Add((Control)locationLabel, 0, 1);
			dataSourceTableLayoutPanel.Controls.Add((Control)locationTextBox, 1, 1);
			dataSourceTableLayoutPanel.Name = "dataSourceTableLayoutPanel";
			componentResourceManager.ApplyResources((object)dataSourceLabel, "dataSourceLabel");
			dataSourceLabel.FlatStyle = FlatStyle.System;
			dataSourceLabel.Name = "dataSourceLabel";
			componentResourceManager.ApplyResources((object)dataSourceTextBox, "dataSourceTextBox");
			dataSourceTextBox.Name = "dataSourceTextBox";
			dataSourceTextBox.Leave += new EventHandler(TrimControlText);
			dataSourceTextBox.TextChanged += new EventHandler(SetDataSource);
			componentResourceManager.ApplyResources((object)locationLabel, "locationLabel");
			locationLabel.FlatStyle = FlatStyle.System;
			locationLabel.Name = "locationLabel";
			componentResourceManager.ApplyResources((object)locationTextBox, "locationTextBox");
			locationTextBox.Name = "locationTextBox";
			locationTextBox.Leave += new EventHandler(TrimControlText);
			locationTextBox.TextChanged += new EventHandler(SetLocation);
			componentResourceManager.ApplyResources((object)logonGroupBox, "logonGroupBox");
			logonGroupBox.Controls.Add((Control)loginTableLayoutPanel);
			logonGroupBox.Controls.Add((Control)nativeSecurityRadioButton);
			logonGroupBox.Controls.Add((Control)integratedSecurityRadioButton);
			logonGroupBox.FlatStyle = FlatStyle.System;
			logonGroupBox.Name = "logonGroupBox";
			logonGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)loginTableLayoutPanel, "loginTableLayoutPanel");
			loginTableLayoutPanel.Controls.Add((Control)subLoginTableLayoutPanel, 0, 0);
			loginTableLayoutPanel.Controls.Add((Control)subSubLoginTableLayoutPanel, 0, 1);
			loginTableLayoutPanel.Name = "loginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)subLoginTableLayoutPanel, "subLoginTableLayoutPanel");
			subLoginTableLayoutPanel.Controls.Add((Control)userNameLabel, 0, 0);
			subLoginTableLayoutPanel.Controls.Add((Control)userNameTextBox, 1, 0);
			subLoginTableLayoutPanel.Controls.Add((Control)passwordLabel, 0, 1);
			subLoginTableLayoutPanel.Controls.Add((Control)passwordTextBox, 1, 1);
			subLoginTableLayoutPanel.Name = "subLoginTableLayoutPanel";
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
			componentResourceManager.ApplyResources((object)subSubLoginTableLayoutPanel, "subSubLoginTableLayoutPanel");
			subSubLoginTableLayoutPanel.Controls.Add((Control)blankPasswordCheckBox, 0, 0);
			subSubLoginTableLayoutPanel.Controls.Add((Control)allowSavingPasswordCheckBox, 1, 0);
			subSubLoginTableLayoutPanel.Name = "subSubLoginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)blankPasswordCheckBox, "blankPasswordCheckBox");
			blankPasswordCheckBox.Name = "blankPasswordCheckBox";
			blankPasswordCheckBox.CheckedChanged += new EventHandler(SetBlankPassword);
			componentResourceManager.ApplyResources((object)allowSavingPasswordCheckBox, "allowSavingPasswordCheckBox");
			allowSavingPasswordCheckBox.Name = "allowSavingPasswordCheckBox";
			allowSavingPasswordCheckBox.CheckedChanged += new EventHandler(SetAllowSavingPassword);
			componentResourceManager.ApplyResources((object)nativeSecurityRadioButton, "nativeSecurityRadioButton");
			nativeSecurityRadioButton.Name = "nativeSecurityRadioButton";
			nativeSecurityRadioButton.CheckedChanged += new EventHandler(SetSecurityOption);
			componentResourceManager.ApplyResources((object)integratedSecurityRadioButton, "integratedSecurityRadioButton");
			integratedSecurityRadioButton.Name = "integratedSecurityRadioButton";
			integratedSecurityRadioButton.CheckedChanged += new EventHandler(SetSecurityOption);
			componentResourceManager.ApplyResources((object)initialCatalogLabel, "initialCatalogLabel");
			initialCatalogLabel.FlatStyle = FlatStyle.System;
			initialCatalogLabel.Name = "initialCatalogLabel";
			componentResourceManager.ApplyResources((object)initialCatalogComboBox, "initialCatalogComboBox");
			initialCatalogComboBox.AutoCompleteMode = AutoCompleteMode.Append;
			initialCatalogComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
			initialCatalogComboBox.FormattingEnabled = true;
			initialCatalogComboBox.Name = "initialCatalogComboBox";
			initialCatalogComboBox.Leave += new EventHandler(TrimControlText);
			initialCatalogComboBox.TextChanged += new EventHandler(SetInitialCatalog);
			initialCatalogComboBox.KeyDown += new KeyEventHandler(HandleComboBoxDownKey);
			initialCatalogComboBox.DropDown += new EventHandler(EnumerateCatalogs);
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add((Control)initialCatalogComboBox);
			Controls.Add((Control)initialCatalogLabel);
			Controls.Add((Control)logonGroupBox);
			Controls.Add((Control)dataSourceGroupBox);
			Controls.Add((Control)providerTableLayoutPanel);
			Controls.Add((Control)providerLabel);
			MinimumSize = new Size(350, 323);
			Name = nameof(OleDBConnectionUIControl);
			providerTableLayoutPanel.ResumeLayout(false);
			providerTableLayoutPanel.PerformLayout();
			dataSourceGroupBox.ResumeLayout(false);
			dataSourceGroupBox.PerformLayout();
			dataSourceTableLayoutPanel.ResumeLayout(false);
			dataSourceTableLayoutPanel.PerformLayout();
			logonGroupBox.ResumeLayout(false);
			logonGroupBox.PerformLayout();
			loginTableLayoutPanel.ResumeLayout(false);
			loginTableLayoutPanel.PerformLayout();
			subLoginTableLayoutPanel.ResumeLayout(false);
			subLoginTableLayoutPanel.PerformLayout();
			subSubLoginTableLayoutPanel.ResumeLayout(false);
			subSubLoginTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}

		private struct ProviderStruct
		{
			private string _progId;
			private string _description;

			public ProviderStruct(string progId, string description)
			{
				_progId = progId;
				_description = description;
			}

			public string ProgId => _progId;

			public string Description => _description;

			public override string ToString() => _description;
		}
	}
}
