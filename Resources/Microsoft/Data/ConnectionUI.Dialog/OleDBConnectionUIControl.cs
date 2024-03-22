using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Design;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
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
			this.InitializeComponent();
			this.RightToLeft = RightToLeft.Inherit;
		}

		public void Initialize(IDataConnectionProperties connectionProperties)
		{
			this.Initialize(connectionProperties, false);
		}

		public void Initialize(
		  IDataConnectionProperties connectionProperties,
		  bool disableProviderSelection)
		{
			if (!(connectionProperties is OleDBConnectionProperties))
				throw new ArgumentException(SR.OleDBConnectionUIControl_InvalidConnectionProperties);
			this.EnumerateProviders();
			this.providerComboBox.Enabled = !disableProviderSelection;
			this.dataLinksButton.Enabled = false;
			this.dataSourceGroupBox.Enabled = false;
			this.logonGroupBox.Enabled = false;
			this.initialCatalogLabel.Enabled = false;
			this.initialCatalogComboBox.Enabled = false;
			this._connectionProperties = connectionProperties;
		}

		public void LoadProperties()
		{
			this._loading = true;
			if (this.Properties["Provider"] is string property && property.Length > 0)
			{
				object obj = (object)null;
				foreach (OleDBConnectionUIControl.ProviderStruct providerStruct in this.providerComboBox.Items)
				{
					if (providerStruct.ProgId.Equals(property))
					{
						obj = (object)providerStruct;
						break;
					}
					if (providerStruct.ProgId.StartsWith(property + ".", StringComparison.OrdinalIgnoreCase) && (obj == null || providerStruct.ProgId.CompareTo(((OleDBConnectionUIControl.ProviderStruct)obj).ProgId) > 0))
						obj = (object)providerStruct;
				}
				this.providerComboBox.SelectedItem = obj;
			}
			else
				this.providerComboBox.SelectedItem = (object)null;
			if (this.Properties.Contains("Data Source") && this.Properties["Data Source"] is string)
				this.dataSourceTextBox.Text = this.Properties["Data Source"] as string;
			else
				this.dataSourceTextBox.Text = (string)null;
			if (this.Properties.Contains("Location") && this.Properties["Location"] is string)
				this.locationTextBox.Text = this.Properties["Location"] as string;
			else
				this.locationTextBox.Text = (string)null;
			if (this.Properties.Contains("Integrated Security") && this.Properties["Integrated Security"] is string && (this.Properties["Integrated Security"] as string).Length > 0)
				this.integratedSecurityRadioButton.Checked = true;
			else
				this.nativeSecurityRadioButton.Checked = true;
			if (this.Properties.Contains("User ID") && this.Properties["User ID"] is string)
				this.userNameTextBox.Text = this.Properties["User ID"] as string;
			else
				this.userNameTextBox.Text = (string)null;
			if (this.Properties.Contains("Password") && this.Properties["Password"] is string)
			{
				this.passwordTextBox.Text = this.Properties["Password"] as string;
				this.blankPasswordCheckBox.Checked = this.passwordTextBox.Text.Length == 0;
			}
			else
			{
				this.passwordTextBox.Text = (string)null;
				this.blankPasswordCheckBox.Checked = false;
			}
			this.allowSavingPasswordCheckBox.Checked = this.Properties.Contains("Persist Security Info") && this.Properties["Persist Security Info"] is bool && (bool)this.Properties["Persist Security Info"];
			if (this.Properties.Contains("Initial Catalog") && this.Properties["Initial Catalog"] is string)
				this.initialCatalogComboBox.Text = this.Properties["Initial Catalog"] as string;
			else
				this.initialCatalogComboBox.Text = (string)null;
			this._loading = false;
		}

		public override Size GetPreferredSize(Size proposedSize)
		{
			Size preferredSize = base.GetPreferredSize(proposedSize);
			Padding padding = this.logonGroupBox.Padding;
			int left1 = padding.Left;
			padding = this.loginTableLayoutPanel.Margin;
			int left2 = padding.Left;
			int num1 = left1 + left2;
			padding = this.blankPasswordCheckBox.Margin;
			int left3 = padding.Left;
			int num2 = num1 + left3 + this.blankPasswordCheckBox.Width;
			padding = this.blankPasswordCheckBox.Margin;
			int right1 = padding.Right;
			int num3 = num2 + right1;
			padding = this.allowSavingPasswordCheckBox.Margin;
			int left4 = padding.Left;
			int num4 = num3 + left4 + this.allowSavingPasswordCheckBox.Width;
			padding = this.allowSavingPasswordCheckBox.Margin;
			int right2 = padding.Right;
			int num5 = num4 + right2;
			padding = this.loginTableLayoutPanel.Margin;
			int right3 = padding.Right;
			int num6 = num5 + right3;
			padding = this.logonGroupBox.Padding;
			int right4 = padding.Right;
			int width = num6 + right4;
			if (width > preferredSize.Width)
				preferredSize = new Size(width, preferredSize.Height);
			return preferredSize;
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (this.ParentForm != null && this.ParentForm.RightToLeftLayout && this.RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl((Control)this.providerLabel, (Control)this.providerTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)this.integratedSecurityRadioButton);
				LayoutUtils.MirrorControl((Control)this.nativeSecurityRadioButton);
				LayoutUtils.MirrorControl((Control)this.loginTableLayoutPanel);
				LayoutUtils.MirrorControl((Control)this.initialCatalogLabel, (Control)this.initialCatalogComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl((Control)this.initialCatalogLabel, (Control)this.initialCatalogComboBox);
				LayoutUtils.UnmirrorControl((Control)this.loginTableLayoutPanel);
				LayoutUtils.UnmirrorControl((Control)this.nativeSecurityRadioButton);
				LayoutUtils.UnmirrorControl((Control)this.integratedSecurityRadioButton);
				LayoutUtils.UnmirrorControl((Control)this.providerLabel, (Control)this.providerTableLayoutPanel);
			}
		}

		protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
		{
			Size size = this.Size;
			this.MinimumSize = Size.Empty;
			base.ScaleControl(factor, specified);
			this.MinimumSize = new Size((int)Math.Round((double)size.Width * (double)factor.Width), (int)Math.Round((double)size.Height * (double)factor.Height));
		}

		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			if (this.Parent != null)
				return;
			this.OnFontChanged(e);
		}

		private void EnumerateProviders()
		{
			Cursor current = Cursor.Current;
			OleDbDataReader oleDbDataReader = (OleDbDataReader)null;
			try
			{
				Cursor.Current = Cursors.WaitCursor;
				oleDbDataReader = OleDbEnumerator.GetEnumerator(System.Type.GetTypeFromCLSID(NativeMethods.CLSID_OLEDB_ENUMERATOR));
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
					this.providerComboBox.Items.Add((object)new OleDBConnectionUIControl.ProviderStruct(keyValuePair.Key, dictionary1[keyValuePair.Value]));
			}
			finally
			{
				oleDbDataReader?.Dispose();
				Cursor.Current = current;
			}
		}

		private void SetProvider(object sender, EventArgs e)
		{
			if (this.providerComboBox.SelectedItem is OleDBConnectionUIControl.ProviderStruct)
			{
				if (!this._loading)
					this.Properties["Provider"] = (object)((OleDBConnectionUIControl.ProviderStruct)this.providerComboBox.SelectedItem).ProgId;
				foreach (PropertyDescriptor property in TypeDescriptor.GetProperties((object)this.Properties))
				{
					if (property.Category.Equals(CategoryAttribute.Default.Category, StringComparison.CurrentCulture))
						this.Properties.Remove(property.DisplayName);
				}
				this.dataLinksButton.Enabled = true;
				this.dataSourceGroupBox.Enabled = true;
				this.logonGroupBox.Enabled = true;
				this.loginTableLayoutPanel.Enabled = true;
				this.initialCatalogLabel.Enabled = true;
				this.initialCatalogComboBox.Enabled = true;
				this.dataSourceLabel.Enabled = false;
				this.dataSourceTextBox.Enabled = false;
				this.locationLabel.Enabled = false;
				this.locationTextBox.Enabled = false;
				this.integratedSecurityRadioButton.Enabled = false;
				this.nativeSecurityRadioButton.Enabled = false;
				this.userNameLabel.Enabled = false;
				this.userNameTextBox.Enabled = false;
				this.passwordLabel.Enabled = false;
				this.passwordTextBox.Enabled = false;
				this.blankPasswordCheckBox.Enabled = false;
				this.allowSavingPasswordCheckBox.Enabled = false;
				this.initialCatalogLabel.Enabled = false;
				this.initialCatalogComboBox.Enabled = false;
				PropertyDescriptorCollection properties = TypeDescriptor.GetProperties((object)this.Properties);
				PropertyDescriptor propertyDescriptor1;
				if ((propertyDescriptor1 = properties["DataSource"]) != null && propertyDescriptor1.IsBrowsable)
				{
					this.dataSourceLabel.Enabled = true;
					this.dataSourceTextBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor2;
				if ((propertyDescriptor2 = properties["Location"]) != null && propertyDescriptor2.IsBrowsable)
				{
					this.locationLabel.Enabled = true;
					this.locationTextBox.Enabled = true;
				}
				this.dataSourceGroupBox.Enabled = this.dataSourceTextBox.Enabled || this.locationTextBox.Enabled;
				PropertyDescriptor propertyDescriptor3;
				if ((propertyDescriptor3 = properties["Integrated Security"]) != null && propertyDescriptor3.IsBrowsable)
					this.integratedSecurityRadioButton.Enabled = true;
				PropertyDescriptor propertyDescriptor4;
				if ((propertyDescriptor4 = properties["User ID"]) != null && propertyDescriptor4.IsBrowsable)
				{
					this.userNameLabel.Enabled = true;
					this.userNameTextBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor5;
				if ((propertyDescriptor5 = properties["Password"]) != null && propertyDescriptor5.IsBrowsable)
				{
					this.passwordLabel.Enabled = true;
					this.passwordTextBox.Enabled = true;
					this.blankPasswordCheckBox.Enabled = true;
				}
				PropertyDescriptor propertyDescriptor6;
				if (this.passwordTextBox.Enabled && (propertyDescriptor6 = properties["PersistSecurityInfo"]) != null && propertyDescriptor6.IsBrowsable)
					this.allowSavingPasswordCheckBox.Enabled = true;
				this.loginTableLayoutPanel.Enabled = this.userNameTextBox.Enabled || this.passwordTextBox.Enabled;
				this.nativeSecurityRadioButton.Enabled = this.loginTableLayoutPanel.Enabled;
				this.logonGroupBox.Enabled = this.integratedSecurityRadioButton.Enabled || this.nativeSecurityRadioButton.Enabled;
				PropertyDescriptor propertyDescriptor7;
				if ((propertyDescriptor7 = properties["Initial Catalog"]) != null && propertyDescriptor7.IsBrowsable)
				{
					this.initialCatalogLabel.Enabled = true;
					this.initialCatalogComboBox.Enabled = true;
				}
			}
			else
			{
				if (!this._loading)
					this.Properties["Provider"] = (object)null;
				this.dataLinksButton.Enabled = false;
				this.dataSourceGroupBox.Enabled = false;
				this.logonGroupBox.Enabled = false;
				this.initialCatalogLabel.Enabled = false;
				this.initialCatalogComboBox.Enabled = false;
			}
			if (!this._loading)
				this.LoadProperties();
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetProviderDropDownWidth(object sender, EventArgs e)
		{
			if (this.providerComboBox.Items.Count > 0)
			{
				int num = 0;
				using (Graphics dc = Graphics.FromHwnd(this.providerComboBox.Handle))
				{
					foreach (OleDBConnectionUIControl.ProviderStruct providerStruct in this.providerComboBox.Items)
					{
						int width = TextRenderer.MeasureText((IDeviceContext)dc, providerStruct.Description, this.providerComboBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak).Width;
						if (width > num)
							num = width;
					}
				}
				this.providerComboBox.DropDownWidth = num + 3;
				if (this.providerComboBox.Items.Count <= this.providerComboBox.MaxDropDownItems)
					return;
				this.providerComboBox.DropDownWidth += SystemInformation.VerticalScrollBarWidth;
			}
			else
				this.providerComboBox.DropDownWidth = Math.Max(1, this.providerComboBox.Width);
		}

		private void ShowDataLinks(object sender, EventArgs e)
		{
			try
			{
				NativeMethods.IDataInitialize instance = Activator.CreateInstance(System.Type.GetTypeFromCLSID(NativeMethods.CLSID_DataLinks)) as NativeMethods.IDataInitialize;
				object ppDataSource = (object)null;
				instance.GetDataSource((object)null, 1, this.Properties.ToFullString(), ref NativeMethods.IID_IUnknown, ref ppDataSource);
				((NativeMethods.IDBPromptInitialize)instance).PromptDataSource((object)null, this.ParentForm.Handle, 18, 0, IntPtr.Zero, (string)null, ref NativeMethods.IID_IUnknown, ref ppDataSource);
				string ppwszInitString = (string)null;
				instance.GetInitializationString(ppDataSource, true, out ppwszInitString);
				this.Properties.Parse(ppwszInitString);
				this.LoadProperties();
			}
			catch (Exception ex)
			{
				if (ex is COMException comException && comException.ErrorCode == -2147217842)
					return;
				if (this.GetService(typeof(IUIService)) is IUIService service)
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
			if (!this._loading)
				this.Properties["Data Source"] = this.dataSourceTextBox.Text.Trim().Length > 0 ? (object)this.dataSourceTextBox.Text.Trim() : (object)(string)null;
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetLocation(object sender, EventArgs e)
		{
			if (!this._loading)
				this.Properties["Location"] = (object)this.locationTextBox.Text;
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetSecurityOption(object sender, EventArgs e)
		{
			if (!this._loading)
			{
				if (this.integratedSecurityRadioButton.Checked)
				{
					this.Properties["Integrated Security"] = (object)"SSPI";
					this.Properties.Reset("User ID");
					this.Properties.Reset("Password");
					this.Properties.Reset("Persist Security Info");
				}
				else
				{
					this.Properties.Reset("Integrated Security");
					this.SetUserName(sender, e);
					this.SetPassword(sender, e);
					this.SetBlankPassword(sender, e);
					this.SetAllowSavingPassword(sender, e);
				}
			}
			this.loginTableLayoutPanel.Enabled = !this.integratedSecurityRadioButton.Checked;
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetUserName(object sender, EventArgs e)
		{
			if (!this._loading)
				this.Properties["User ID"] = this.userNameTextBox.Text.Trim().Length > 0 ? (object)this.userNameTextBox.Text.Trim() : (object)(string)null;
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetPassword(object sender, EventArgs e)
		{
			if (!this._loading)
			{
				this.Properties["Password"] = this.passwordTextBox.Text.Length > 0 ? (object)this.passwordTextBox.Text : (object)(string)null;
				if (this.passwordTextBox.Text.Length == 0)
					this.Properties.Remove("Password");
				this.passwordTextBox.Text = this.passwordTextBox.Text;
			}
			this.initialCatalogComboBox.Items.Clear();
		}

		private void SetBlankPassword(object sender, EventArgs e)
		{
			if (this.blankPasswordCheckBox.Checked)
			{
				if (!this._loading)
					this.Properties["Password"] = (object)string.Empty;
				this.passwordLabel.Enabled = false;
				this.passwordTextBox.Enabled = false;
			}
			else
			{
				if (!this._loading)
					this.SetPassword(sender, e);
				this.passwordLabel.Enabled = true;
				this.passwordTextBox.Enabled = true;
			}
		}

		private void SetAllowSavingPassword(object sender, EventArgs e)
		{
			if (this._loading)
				return;
			this.Properties["Persist Security Info"] = (object)this.allowSavingPasswordCheckBox.Checked;
		}

		private void HandleComboBoxDownKey(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != System.Windows.Forms.Keys.Down)
				return;
			this.EnumerateCatalogs(sender, (EventArgs)e);
		}

		private void SetInitialCatalog(object sender, EventArgs e)
		{
			if (this._loading)
				return;
			this.Properties["Initial Catalog"] = this.initialCatalogComboBox.Text.Trim().Length > 0 ? (object)this.initialCatalogComboBox.Text.Trim() : (object)(string)null;
			if (this.initialCatalogComboBox.Items.Count != 0 || this._catalogEnumerationThread != null)
				return;
			this._catalogEnumerationThread = new Thread(new ThreadStart(this.EnumerateCatalogs));
			this._catalogEnumerationThread.Start();
		}

		private void EnumerateCatalogs(object sender, EventArgs e)
		{
			if (this.initialCatalogComboBox.Items.Count != 0)
				return;
			Cursor current = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				if (this._catalogEnumerationThread == null || this._catalogEnumerationThread.ThreadState == ThreadState.Stopped)
				{
					this.EnumerateCatalogs();
				}
				else
				{
					if (this._catalogEnumerationThread.ThreadState != ThreadState.Running)
						return;
					this._catalogEnumerationThread.Join();
					this.PopulateInitialCatalogComboBox();
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
				OleDbConnectionStringBuilder connectionStringBuilder = new OleDbConnectionStringBuilder(this.Properties.ToFullString());
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
			this._catalogs = new object[dataTable.Rows.Count];
			for (int index = 0; index < this._catalogs.Length; ++index)
				this._catalogs[index] = dataTable.Rows[index]["CATALOG_NAME"];
			if (Thread.CurrentThread == this._uiThread)
			{
				this.PopulateInitialCatalogComboBox();
			}
			else
			{
				if (!this.IsHandleCreated)
					return;
				this.BeginInvoke((Delegate)new ThreadStart(this.PopulateInitialCatalogComboBox));
			}
		}

		private void PopulateInitialCatalogComboBox()
		{
			if (this.initialCatalogComboBox.Items.Count == 0)
			{
				if (this._catalogs.Length != 0)
					this.initialCatalogComboBox.Items.AddRange(this._catalogs);
				else
					this.initialCatalogComboBox.Items.Add((object)string.Empty);
			}
			this._catalogEnumerationThread = (Thread)null;
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
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(OleDBConnectionUIControl));
			this.providerLabel = new Label();
			this.providerTableLayoutPanel = new TableLayoutPanel();
			this.providerComboBox = new ComboBox();
			this.dataLinksButton = new Button();
			this.dataSourceGroupBox = new GroupBox();
			this.dataSourceTableLayoutPanel = new TableLayoutPanel();
			this.dataSourceLabel = new Label();
			this.dataSourceTextBox = new TextBox();
			this.locationLabel = new Label();
			this.locationTextBox = new TextBox();
			this.logonGroupBox = new GroupBox();
			this.loginTableLayoutPanel = new TableLayoutPanel();
			this.subLoginTableLayoutPanel = new TableLayoutPanel();
			this.userNameLabel = new Label();
			this.userNameTextBox = new TextBox();
			this.passwordLabel = new Label();
			this.passwordTextBox = new TextBox();
			this.subSubLoginTableLayoutPanel = new TableLayoutPanel();
			this.blankPasswordCheckBox = new CheckBox();
			this.allowSavingPasswordCheckBox = new CheckBox();
			this.nativeSecurityRadioButton = new RadioButton();
			this.integratedSecurityRadioButton = new RadioButton();
			this.initialCatalogLabel = new Label();
			this.initialCatalogComboBox = new ComboBox();
			this.providerTableLayoutPanel.SuspendLayout();
			this.dataSourceGroupBox.SuspendLayout();
			this.dataSourceTableLayoutPanel.SuspendLayout();
			this.logonGroupBox.SuspendLayout();
			this.loginTableLayoutPanel.SuspendLayout();
			this.subLoginTableLayoutPanel.SuspendLayout();
			this.subSubLoginTableLayoutPanel.SuspendLayout();
			this.SuspendLayout();
			componentResourceManager.ApplyResources((object)this.providerLabel, "providerLabel");
			this.providerLabel.FlatStyle = FlatStyle.System;
			this.providerLabel.Name = "providerLabel";
			componentResourceManager.ApplyResources((object)this.providerTableLayoutPanel, "providerTableLayoutPanel");
			this.providerTableLayoutPanel.Controls.Add((Control)this.providerComboBox, 0, 0);
			this.providerTableLayoutPanel.Controls.Add((Control)this.dataLinksButton, 1, -1);
			this.providerTableLayoutPanel.Name = "providerTableLayoutPanel";
			componentResourceManager.ApplyResources((object)this.providerComboBox, "providerComboBox");
			this.providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
			this.providerComboBox.FormattingEnabled = true;
			this.providerComboBox.Name = "providerComboBox";
			this.providerComboBox.Sorted = true;
			this.providerComboBox.SelectedIndexChanged += new EventHandler(this.SetProvider);
			this.providerComboBox.DropDown += new EventHandler(this.SetProviderDropDownWidth);
			componentResourceManager.ApplyResources((object)this.dataLinksButton, "dataLinksButton");
			this.dataLinksButton.MinimumSize = new Size(83, 23);
			this.dataLinksButton.Name = "dataLinksButton";
			this.dataLinksButton.Click += new EventHandler(this.ShowDataLinks);
			componentResourceManager.ApplyResources((object)this.dataSourceGroupBox, "dataSourceGroupBox");
			this.dataSourceGroupBox.Controls.Add((Control)this.dataSourceTableLayoutPanel);
			this.dataSourceGroupBox.FlatStyle = FlatStyle.System;
			this.dataSourceGroupBox.Name = "dataSourceGroupBox";
			this.dataSourceGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)this.dataSourceTableLayoutPanel, "dataSourceTableLayoutPanel");
			this.dataSourceTableLayoutPanel.Controls.Add((Control)this.dataSourceLabel, 0, 0);
			this.dataSourceTableLayoutPanel.Controls.Add((Control)this.dataSourceTextBox, 1, 0);
			this.dataSourceTableLayoutPanel.Controls.Add((Control)this.locationLabel, 0, 1);
			this.dataSourceTableLayoutPanel.Controls.Add((Control)this.locationTextBox, 1, 1);
			this.dataSourceTableLayoutPanel.Name = "dataSourceTableLayoutPanel";
			componentResourceManager.ApplyResources((object)this.dataSourceLabel, "dataSourceLabel");
			this.dataSourceLabel.FlatStyle = FlatStyle.System;
			this.dataSourceLabel.Name = "dataSourceLabel";
			componentResourceManager.ApplyResources((object)this.dataSourceTextBox, "dataSourceTextBox");
			this.dataSourceTextBox.Name = "dataSourceTextBox";
			this.dataSourceTextBox.Leave += new EventHandler(this.TrimControlText);
			this.dataSourceTextBox.TextChanged += new EventHandler(this.SetDataSource);
			componentResourceManager.ApplyResources((object)this.locationLabel, "locationLabel");
			this.locationLabel.FlatStyle = FlatStyle.System;
			this.locationLabel.Name = "locationLabel";
			componentResourceManager.ApplyResources((object)this.locationTextBox, "locationTextBox");
			this.locationTextBox.Name = "locationTextBox";
			this.locationTextBox.Leave += new EventHandler(this.TrimControlText);
			this.locationTextBox.TextChanged += new EventHandler(this.SetLocation);
			componentResourceManager.ApplyResources((object)this.logonGroupBox, "logonGroupBox");
			this.logonGroupBox.Controls.Add((Control)this.loginTableLayoutPanel);
			this.logonGroupBox.Controls.Add((Control)this.nativeSecurityRadioButton);
			this.logonGroupBox.Controls.Add((Control)this.integratedSecurityRadioButton);
			this.logonGroupBox.FlatStyle = FlatStyle.System;
			this.logonGroupBox.Name = "logonGroupBox";
			this.logonGroupBox.TabStop = false;
			componentResourceManager.ApplyResources((object)this.loginTableLayoutPanel, "loginTableLayoutPanel");
			this.loginTableLayoutPanel.Controls.Add((Control)this.subLoginTableLayoutPanel, 0, 0);
			this.loginTableLayoutPanel.Controls.Add((Control)this.subSubLoginTableLayoutPanel, 0, 1);
			this.loginTableLayoutPanel.Name = "loginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)this.subLoginTableLayoutPanel, "subLoginTableLayoutPanel");
			this.subLoginTableLayoutPanel.Controls.Add((Control)this.userNameLabel, 0, 0);
			this.subLoginTableLayoutPanel.Controls.Add((Control)this.userNameTextBox, 1, 0);
			this.subLoginTableLayoutPanel.Controls.Add((Control)this.passwordLabel, 0, 1);
			this.subLoginTableLayoutPanel.Controls.Add((Control)this.passwordTextBox, 1, 1);
			this.subLoginTableLayoutPanel.Name = "subLoginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)this.userNameLabel, "userNameLabel");
			this.userNameLabel.FlatStyle = FlatStyle.System;
			this.userNameLabel.Name = "userNameLabel";
			componentResourceManager.ApplyResources((object)this.userNameTextBox, "userNameTextBox");
			this.userNameTextBox.Name = "userNameTextBox";
			this.userNameTextBox.Leave += new EventHandler(this.TrimControlText);
			this.userNameTextBox.TextChanged += new EventHandler(this.SetUserName);
			componentResourceManager.ApplyResources((object)this.passwordLabel, "passwordLabel");
			this.passwordLabel.FlatStyle = FlatStyle.System;
			this.passwordLabel.Name = "passwordLabel";
			componentResourceManager.ApplyResources((object)this.passwordTextBox, "passwordTextBox");
			this.passwordTextBox.Name = "passwordTextBox";
			this.passwordTextBox.UseSystemPasswordChar = true;
			this.passwordTextBox.TextChanged += new EventHandler(this.SetPassword);
			componentResourceManager.ApplyResources((object)this.subSubLoginTableLayoutPanel, "subSubLoginTableLayoutPanel");
			this.subSubLoginTableLayoutPanel.Controls.Add((Control)this.blankPasswordCheckBox, 0, 0);
			this.subSubLoginTableLayoutPanel.Controls.Add((Control)this.allowSavingPasswordCheckBox, 1, 0);
			this.subSubLoginTableLayoutPanel.Name = "subSubLoginTableLayoutPanel";
			componentResourceManager.ApplyResources((object)this.blankPasswordCheckBox, "blankPasswordCheckBox");
			this.blankPasswordCheckBox.Name = "blankPasswordCheckBox";
			this.blankPasswordCheckBox.CheckedChanged += new EventHandler(this.SetBlankPassword);
			componentResourceManager.ApplyResources((object)this.allowSavingPasswordCheckBox, "allowSavingPasswordCheckBox");
			this.allowSavingPasswordCheckBox.Name = "allowSavingPasswordCheckBox";
			this.allowSavingPasswordCheckBox.CheckedChanged += new EventHandler(this.SetAllowSavingPassword);
			componentResourceManager.ApplyResources((object)this.nativeSecurityRadioButton, "nativeSecurityRadioButton");
			this.nativeSecurityRadioButton.Name = "nativeSecurityRadioButton";
			this.nativeSecurityRadioButton.CheckedChanged += new EventHandler(this.SetSecurityOption);
			componentResourceManager.ApplyResources((object)this.integratedSecurityRadioButton, "integratedSecurityRadioButton");
			this.integratedSecurityRadioButton.Name = "integratedSecurityRadioButton";
			this.integratedSecurityRadioButton.CheckedChanged += new EventHandler(this.SetSecurityOption);
			componentResourceManager.ApplyResources((object)this.initialCatalogLabel, "initialCatalogLabel");
			this.initialCatalogLabel.FlatStyle = FlatStyle.System;
			this.initialCatalogLabel.Name = "initialCatalogLabel";
			componentResourceManager.ApplyResources((object)this.initialCatalogComboBox, "initialCatalogComboBox");
			this.initialCatalogComboBox.AutoCompleteMode = AutoCompleteMode.Append;
			this.initialCatalogComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
			this.initialCatalogComboBox.FormattingEnabled = true;
			this.initialCatalogComboBox.Name = "initialCatalogComboBox";
			this.initialCatalogComboBox.Leave += new EventHandler(this.TrimControlText);
			this.initialCatalogComboBox.TextChanged += new EventHandler(this.SetInitialCatalog);
			this.initialCatalogComboBox.KeyDown += new KeyEventHandler(this.HandleComboBoxDownKey);
			this.initialCatalogComboBox.DropDown += new EventHandler(this.EnumerateCatalogs);
			componentResourceManager.ApplyResources((object)this, "$this");
			this.AutoScaleMode = AutoScaleMode.Font;
			this.Controls.Add((Control)this.initialCatalogComboBox);
			this.Controls.Add((Control)this.initialCatalogLabel);
			this.Controls.Add((Control)this.logonGroupBox);
			this.Controls.Add((Control)this.dataSourceGroupBox);
			this.Controls.Add((Control)this.providerTableLayoutPanel);
			this.Controls.Add((Control)this.providerLabel);
			this.MinimumSize = new Size(350, 323);
			this.Name = nameof(OleDBConnectionUIControl);
			this.providerTableLayoutPanel.ResumeLayout(false);
			this.providerTableLayoutPanel.PerformLayout();
			this.dataSourceGroupBox.ResumeLayout(false);
			this.dataSourceGroupBox.PerformLayout();
			this.dataSourceTableLayoutPanel.ResumeLayout(false);
			this.dataSourceTableLayoutPanel.PerformLayout();
			this.logonGroupBox.ResumeLayout(false);
			this.logonGroupBox.PerformLayout();
			this.loginTableLayoutPanel.ResumeLayout(false);
			this.loginTableLayoutPanel.PerformLayout();
			this.subLoginTableLayoutPanel.ResumeLayout(false);
			this.subLoginTableLayoutPanel.PerformLayout();
			this.subSubLoginTableLayoutPanel.ResumeLayout(false);
			this.subSubLoginTableLayoutPanel.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		private struct ProviderStruct
		{
			private string _progId;
			private string _description;

			public ProviderStruct(string progId, string description)
			{
				this._progId = progId;
				this._description = description;
			}

			public string ProgId => this._progId;

			public string Description => this._description;

			public override string ToString() => this._description;
		}
	}
}
