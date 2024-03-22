using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Design;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
    public class DataConnectionDialog : Form
    {
        private Size _initialContainerControlSize = Size.Empty;
        private bool _showingDialog;
        private Label _headerLabel;
        private bool _translateHelpButton = true;
        private string _chooseDataSourceTitle;
        private string _chooseDataSourceHeaderLabel = string.Empty;
        private string _chooseDataSourceAcceptText;
        private string _changeDataSourceTitle;
        private string _changeDataSourceHeaderLabel = string.Empty;
        private ICollection<DataSource> _dataSources;
        private DataSource _unspecifiedDataSource = DataSource.CreateUnspecified();
        private DataSource _selectedDataSource;
        private IDictionary<DataSource, DataProvider> _dataProviderSelections = (IDictionary<DataSource, DataProvider>)new Dictionary<DataSource, DataProvider>();
        private bool _saveSelection = true;
        private IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionUIControl>> _connectionUIControlTable = (IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionUIControl>>)new Dictionary<DataSource, IDictionary<DataProvider, IDataConnectionUIControl>>();
        private IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionProperties>> _connectionPropertiesTable = (IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionProperties>>)new Dictionary<DataSource, IDictionary<DataProvider, IDataConnectionProperties>>();
        private IContainer components;
        private Label dataSourceLabel;
        private TableLayoutPanel dataSourceTableLayoutPanel;
        private TextBox dataSourceTextBox;
        private ToolTip dataProviderToolTip;
        private Button changeDataSourceButton;
        private ContainerControl containerControl;
        private Button advancedButton;
        private Panel separatorPanel;
        private Button testConnectionButton;
        private TableLayoutPanel buttonsTableLayoutPanel;
        private Button acceptButton;
        private Button cancelButton;

        public DataConnectionDialog()
        {
            InitializeComponent();
            dataSourceTextBox.Width = 0;
            components.Add((IComponent)new UserPreferenceChangedHandler((Form)this));
            ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(DataConnectionSourceDialog));
            _chooseDataSourceTitle = componentResourceManager.GetString("$this.Text");
            _chooseDataSourceAcceptText = componentResourceManager.GetString("okButton.Text");
            _changeDataSourceTitle = SR.DataConnectionDialog_ChangeDataSourceTitle;
            _dataSources = (ICollection<DataSource>)new DataConnectionDialog.DataSourceCollection(this);
        }

        public static DialogResult Show(DataConnectionDialog dialog)
        {
            return DataConnectionDialog.Show(dialog, (IWin32Window)null);
        }

        public static DialogResult Show(DataConnectionDialog dialog, IWin32Window owner)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));
            if (dialog.DataSources.Count == 0)
                throw new InvalidOperationException(SR.DataConnectionDialog_NoDataSourcesAvailable);
            foreach (DataSource dataSource in (IEnumerable<DataSource>)dialog.DataSources)
            {
                if (dataSource.Providers.Count == 0)
                {
                    // ISSUE: reference to a compiler-generated method
                    throw new InvalidOperationException(SRHelper.DataConnectionDialog_NoDataProvidersForDataSource(dataSource.DisplayName.Replace("'", "''")));
                }
            }
            Application.ThreadException += new ThreadExceptionEventHandler(dialog.HandleDialogException);
            dialog._showingDialog = true;
            try
            {
                if (dialog.SelectedDataSource == null || dialog.SelectedDataProvider == null)
                {
                    DataConnectionSourceDialog connectionSourceDialog = new DataConnectionSourceDialog(dialog);
                    connectionSourceDialog.Title = dialog.ChooseDataSourceTitle;
                    connectionSourceDialog.HeaderLabel = dialog.ChooseDataSourceHeaderLabel;
                    (connectionSourceDialog.AcceptButton as Button).Text = dialog.ChooseDataSourceAcceptText;
                    if (dialog.Container != null)
                        dialog.Container.Add((IComponent)connectionSourceDialog);
                    try
                    {
                        if (owner == null)
                            connectionSourceDialog.StartPosition = FormStartPosition.CenterScreen;
                        int num = (int)connectionSourceDialog.ShowDialog(owner);
                        if (dialog.SelectedDataSource != null)
                        {
                            if (dialog.SelectedDataProvider != null)
                                goto label_26;
                        }
                        return DialogResult.Cancel;
                    }
                    finally
                    {
                        if (dialog.Container != null)
                            dialog.Container.Remove((IComponent)connectionSourceDialog);
                        connectionSourceDialog.Dispose();
                    }
                }
                else
                    dialog._saveSelection = false;
                label_26:
                if (owner == null)
                    dialog.StartPosition = FormStartPosition.CenterScreen;
                DialogResult dialogResult;
                while (true)
                {
                    dialogResult = dialog.ShowDialog(owner);
                    if (dialogResult == DialogResult.Ignore)
                    {
                        DataConnectionSourceDialog connectionSourceDialog = new DataConnectionSourceDialog(dialog);
                        connectionSourceDialog.Title = dialog.ChangeDataSourceTitle;
                        connectionSourceDialog.HeaderLabel = dialog.ChangeDataSourceHeaderLabel;
                        if (dialog.Container != null)
                            dialog.Container.Add((IComponent)connectionSourceDialog);
                        try
                        {
                            if (owner == null)
                                connectionSourceDialog.StartPosition = FormStartPosition.CenterScreen;
                            connectionSourceDialog.ShowDialog(owner);
                        }
                        finally
                        {
                            if (dialog.Container != null)
                                dialog.Container.Remove((IComponent)connectionSourceDialog);
                            connectionSourceDialog.Dispose();
                        }
                    }
                    else
                        break;
                }
                return dialogResult;
            }
            finally
            {
                dialog._showingDialog = false;
                Application.ThreadException -= new ThreadExceptionEventHandler(dialog.HandleDialogException);
            }
        }

        public string Title
        {
            get => Text;
            set => Text = value;
        }

        public string HeaderLabel
        {
            get => _headerLabel == null ? string.Empty : _headerLabel.Text;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (_headerLabel == null)
                {
                    switch (value)
                    {
                        case null:
                            return;
                        case "":
                            return;
                    }
                }
                if (_headerLabel != null && value == _headerLabel.Text)
                    return;
                if (value != null && value.Length > 0)
                {
                    if (_headerLabel == null)
                    {
                        _headerLabel = new Label();
                        _headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        _headerLabel.FlatStyle = FlatStyle.System;
                        _headerLabel.Location = new Point(12, 12);
                        _headerLabel.Margin = new Padding(3);
                        _headerLabel.Name = "dataSourceLabel";
                        _headerLabel.Width = dataSourceTableLayoutPanel.Width;
                        _headerLabel.TabIndex = 100;
                        Controls.Add((Control)_headerLabel);
                    }
                    _headerLabel.Text = value;
                    MinimumSize = Size.Empty;
                    _headerLabel.Height = LayoutUtils.GetPreferredLabelHeight(_headerLabel);
                    int num = _headerLabel.Bottom + _headerLabel.Margin.Bottom + dataSourceLabel.Margin.Top - dataSourceLabel.Top;
                    containerControl.Anchor &= ~AnchorStyles.Bottom;
                    Height += num;
                    containerControl.Anchor |= AnchorStyles.Bottom;
                    containerControl.Top += num;
                    dataSourceTableLayoutPanel.Top += num;
                    dataSourceLabel.Top += num;
                    MinimumSize = Size;
                }
                else
                {
                    if (_headerLabel == null)
                        return;
                    int height = _headerLabel.Height;
                    try
                    {
                        Controls.Remove((Control)_headerLabel);
                    }
                    finally
                    {
                        _headerLabel.Dispose();
                        _headerLabel = (Label)null;
                    }
                    MinimumSize = Size.Empty;
                    dataSourceLabel.Top -= height;
                    dataSourceTableLayoutPanel.Top -= height;
                    containerControl.Top -= height;
                    containerControl.Anchor &= ~AnchorStyles.Bottom;
                    Height -= height;
                    containerControl.Anchor |= AnchorStyles.Bottom;
                    MinimumSize = Size;
                }
            }
        }

        public bool TranslateHelpButton
        {
            get => _translateHelpButton;
            set => _translateHelpButton = value;
        }

        public string ChooseDataSourceTitle
        {
            get => _chooseDataSourceTitle;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (value == null)
                    value = string.Empty;
                if (value == _chooseDataSourceTitle)
                    return;
                _chooseDataSourceTitle = value;
            }
        }

        public string ChooseDataSourceHeaderLabel
        {
            get => _chooseDataSourceHeaderLabel;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (value == null)
                    value = string.Empty;
                if (value == _chooseDataSourceHeaderLabel)
                    return;
                _chooseDataSourceHeaderLabel = value;
            }
        }

        public string ChooseDataSourceAcceptText
        {
            get => _chooseDataSourceAcceptText;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (value == null)
                    value = string.Empty;
                if (value == _chooseDataSourceAcceptText)
                    return;
                _chooseDataSourceAcceptText = value;
            }
        }

        public string ChangeDataSourceTitle
        {
            get => _changeDataSourceTitle;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (value == null)
                    value = string.Empty;
                if (value == _changeDataSourceTitle)
                    return;
                _changeDataSourceTitle = value;
            }
        }

        public string ChangeDataSourceHeaderLabel
        {
            get => _changeDataSourceHeaderLabel;
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (value == null)
                    value = string.Empty;
                if (value == _changeDataSourceHeaderLabel)
                    return;
                _changeDataSourceHeaderLabel = value;
            }
        }

        public ICollection<DataSource> DataSources => _dataSources;

        public DataSource UnspecifiedDataSource => _unspecifiedDataSource;

        public DataSource SelectedDataSource
        {
            get
            {
                if (_dataSources == null)
                    return (DataSource)null;
                switch (_dataSources.Count)
                {
                    case 0:
                        return (DataSource)null;
                    case 1:
                        IEnumerator<DataSource> enumerator = _dataSources.GetEnumerator();
                        enumerator.MoveNext();
                        return enumerator.Current;
                    default:
                        return _selectedDataSource;
                }
            }
            set
            {
                if (SelectedDataSource == value)
                    return;
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                SetSelectedDataSource(value, false);
            }
        }

        public DataProvider SelectedDataProvider
        {
            get => GetSelectedDataProvider(SelectedDataSource);
            set
            {
                if (SelectedDataProvider == value)
                    return;
                if (SelectedDataSource == null)
                    throw new InvalidOperationException(SR.DataConnectionDialog_NoDataSourceSelected);
                SetSelectedDataProvider(SelectedDataSource, value);
            }
        }

        public DataProvider GetSelectedDataProvider(DataSource dataSource)
        {
            if (dataSource == null)
                return (DataProvider)null;
            switch (dataSource.Providers.Count)
            {
                case 0:
                    return (DataProvider)null;
                case 1:
                    IEnumerator<DataProvider> enumerator = dataSource.Providers.GetEnumerator();
                    enumerator.MoveNext();
                    return enumerator.Current;
                default:
                    return !_dataProviderSelections.ContainsKey(dataSource) ? (DataProvider)null : _dataProviderSelections[dataSource];
            }
        }

        public void SetSelectedDataProvider(DataSource dataSource, DataProvider dataProvider)
        {
            if (GetSelectedDataProvider(dataSource) == dataProvider)
                return;
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (_showingDialog)
                throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
            SetSelectedDataProvider(dataSource, dataProvider, false);
        }

        public bool SaveSelection
        {
            get => _saveSelection;
            set => _saveSelection = value;
        }

        public string DisplayConnectionString
        {
            get
            {
                string str = (string)null;
                if (ConnectionProperties != null)
                {
                    try
                    {
                        str = ConnectionProperties.ToDisplayString();
                    }
                    catch
                    {
                    }
                }
                return str ?? string.Empty;
            }
        }

        public string ConnectionString
        {
            get
            {
                string str = (string)null;
                if (ConnectionProperties != null)
                {
                    try
                    {
                        str = ConnectionProperties.ToString();
                    }
                    catch
                    {
                    }
                }
                return str ?? string.Empty;
            }
            set
            {
                if (_showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (SelectedDataProvider == null)
                    throw new InvalidOperationException(SR.DataConnectionDialog_NoDataProviderSelected);
                if (ConnectionProperties == null)
                    return;
                ConnectionProperties.Parse(value);
            }
        }

        public string AcceptButtonText
        {
            get => acceptButton.Text;
            set => acceptButton.Text = value;
        }

        public event EventHandler VerifySettings;

        public event EventHandler<ContextHelpEventArgs> ContextHelpRequested;

        public event ThreadExceptionEventHandler DialogException;

        internal UserControl ConnectionUIControl
        {
            get
            {
                if (SelectedDataProvider == null)
                    return (UserControl)null;
                if (!_connectionUIControlTable.ContainsKey(SelectedDataSource))
                    _connectionUIControlTable[SelectedDataSource] = (IDictionary<DataProvider, IDataConnectionUIControl>)new Dictionary<DataProvider, IDataConnectionUIControl>();
                if (!_connectionUIControlTable[SelectedDataSource].ContainsKey(SelectedDataProvider))
                {
                    IDataConnectionUIControl connectionUiControl = (IDataConnectionUIControl)null;
                    UserControl userControl = (UserControl)null;
                    try
                    {
                        connectionUiControl = SelectedDataSource != UnspecifiedDataSource ? SelectedDataProvider.CreateConnectionUIControl(SelectedDataSource) : SelectedDataProvider.CreateConnectionUIControl();
                        userControl = connectionUiControl as UserControl;
                    }
                    catch
                    {
                    }
                    if (connectionUiControl == null || userControl == null)
                    {
                        connectionUiControl = (IDataConnectionUIControl)new DataConnectionDialog.PropertyGridUIControl();
                        userControl = connectionUiControl as UserControl;
                    }
                    userControl.Location = Point.Empty;
                    userControl.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    userControl.AutoSize = false;
                    try
                    {
                        connectionUiControl.Initialize(ConnectionProperties);
                    }
                    catch
                    {
                    }
                    _connectionUIControlTable[SelectedDataSource][SelectedDataProvider] = connectionUiControl;
                    components.Add((IComponent)userControl);
                }
                return _connectionUIControlTable[SelectedDataSource][SelectedDataProvider] as UserControl;
            }
        }

        internal IDataConnectionProperties ConnectionProperties
        {
            get
            {
                if (SelectedDataProvider == null)
                    return (IDataConnectionProperties)null;
                if (!_connectionPropertiesTable.ContainsKey(SelectedDataSource))
                    _connectionPropertiesTable[SelectedDataSource] = (IDictionary<DataProvider, IDataConnectionProperties>)new Dictionary<DataProvider, IDataConnectionProperties>();
                if (!_connectionPropertiesTable[SelectedDataSource].ContainsKey(SelectedDataProvider))
                {
                    IDataConnectionProperties connectionProperties = (IDataConnectionProperties)null;
                    try
                    {
                        connectionProperties = SelectedDataSource != UnspecifiedDataSource ? SelectedDataProvider.CreateConnectionProperties(SelectedDataSource) : SelectedDataProvider.CreateConnectionProperties();
                    }
                    catch
                    {
                    }
                    if (connectionProperties == null)
                        connectionProperties = (IDataConnectionProperties)new DataConnectionDialog.BasicConnectionProperties();
                    try
                    {
                        connectionProperties.PropertyChanged += new EventHandler(ConfigureAcceptButton);
                    }
                    catch
                    {
                    }
                    _connectionPropertiesTable[SelectedDataSource][SelectedDataProvider] = connectionProperties;
                }
                return _connectionPropertiesTable[SelectedDataSource][SelectedDataProvider];
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!_showingDialog)
                throw new NotSupportedException(SR.DataConnectionDialog_ShowDialogNotSupported);
            ConfigureDataSourceTextBox();
            ConfigureChangeDataSourceButton();
            ConfigureContainerControl();
            ConfigureAcceptButton((object)this, EventArgs.Empty);
            base.OnLoad(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (ConnectionUIControl == null)
                return;
            ConnectionUIControl.Focus();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            dataSourceTableLayoutPanel.Anchor &= ~AnchorStyles.Right;
            containerControl.Anchor &= ~(AnchorStyles.Bottom | AnchorStyles.Right);
            advancedButton.Anchor |= AnchorStyles.Top | AnchorStyles.Left;
            advancedButton.Anchor &= ~(AnchorStyles.Bottom | AnchorStyles.Right);
            separatorPanel.Anchor |= AnchorStyles.Top;
            separatorPanel.Anchor &= ~(AnchorStyles.Bottom | AnchorStyles.Right);
            testConnectionButton.Anchor |= AnchorStyles.Top;
            testConnectionButton.Anchor &= ~AnchorStyles.Bottom;
            buttonsTableLayoutPanel.Anchor |= AnchorStyles.Top | AnchorStyles.Left;
            buttonsTableLayoutPanel.Anchor &= ~(AnchorStyles.Bottom | AnchorStyles.Right);
            Size size = Size - SizeFromClientSize(new Size(containerControl.Right + containerControl.Margin.Right + Padding.Right, buttonsTableLayoutPanel.Bottom + buttonsTableLayoutPanel.Margin.Bottom + Padding.Bottom));
            MinimumSize = MinimumSize - size;
            Size = Size - size;
            buttonsTableLayoutPanel.Anchor |= AnchorStyles.Bottom | AnchorStyles.Right;
            buttonsTableLayoutPanel.Anchor &= ~(AnchorStyles.Top | AnchorStyles.Left);
            testConnectionButton.Anchor |= AnchorStyles.Bottom;
            testConnectionButton.Anchor &= ~AnchorStyles.Top;
            separatorPanel.Anchor |= AnchorStyles.Bottom | AnchorStyles.Right;
            separatorPanel.Anchor &= ~AnchorStyles.Top;
            advancedButton.Anchor |= AnchorStyles.Bottom | AnchorStyles.Right;
            advancedButton.Anchor &= ~(AnchorStyles.Top | AnchorStyles.Left);
            containerControl.Anchor |= AnchorStyles.Bottom | AnchorStyles.Right;
            dataSourceTableLayoutPanel.Anchor |= AnchorStyles.Right;
        }

        protected virtual void OnVerifySettings(EventArgs e)
        {
            if (VerifySettings == null)
                return;
            VerifySettings((object)this, e);
        }

        protected internal virtual void OnContextHelpRequested(ContextHelpEventArgs e)
        {
            if (ContextHelpRequested != null)
                ContextHelpRequested((object)this, e);
            if (e.Handled)
                return;
            ShowError((string)null, SR.DataConnectionDialog_NoHelpAvailable);
            e.Handled = true;
        }

        protected override void OnHelpRequested(HelpEventArgs hevent)
        {
            Control control = (Control)this;
            while (control is ContainerControl containerControl && !(containerControl is IDataConnectionUIControl) && containerControl.ActiveControl != null)
                control = containerControl.ActiveControl;
            DataConnectionDialogContext context = DataConnectionDialogContext.Main;
            if (control == dataSourceTextBox)
                context = DataConnectionDialogContext.MainDataSourceTextBox;
            if (control == changeDataSourceButton)
                context = DataConnectionDialogContext.MainChangeDataSourceButton;
            if (control is IDataConnectionUIControl)
            {
                context = DataConnectionDialogContext.MainConnectionUIControl;
                if (ConnectionUIControl is SqlConnectionUIControl)
                    context = DataConnectionDialogContext.MainSqlConnectionUIControl;
                if (ConnectionUIControl is SqlFileConnectionUIControl)
                    context = DataConnectionDialogContext.MainSqlFileConnectionUIControl;
                if (ConnectionUIControl is OracleConnectionUIControl)
                    context = DataConnectionDialogContext.MainOracleConnectionUIControl;
                if (ConnectionUIControl is AccessConnectionUIControl)
                    context = DataConnectionDialogContext.MainAccessConnectionUIControl;
                if (ConnectionUIControl is OleDBConnectionUIControl)
                    context = DataConnectionDialogContext.MainOleDBConnectionUIControl;
                if (ConnectionUIControl is OdbcConnectionUIControl)
                    context = DataConnectionDialogContext.MainOdbcConnectionUIControl;
                if (ConnectionUIControl is DataConnectionDialog.PropertyGridUIControl)
                    context = DataConnectionDialogContext.MainGenericConnectionUIControl;
            }
            if (control == advancedButton)
                context = DataConnectionDialogContext.MainAdvancedButton;
            if (control == testConnectionButton)
                context = DataConnectionDialogContext.MainTestConnectionButton;
            if (control == acceptButton)
                context = DataConnectionDialogContext.MainAcceptButton;
            if (control == cancelButton)
                context = DataConnectionDialogContext.MainCancelButton;
            ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
            OnContextHelpRequested(e);
            hevent.Handled = e.Handled;
            if (e.Handled)
                return;
            base.OnHelpRequested(hevent);
        }

        protected virtual void OnDialogException(ThreadExceptionEventArgs e)
        {
            if (DialogException != null)
                DialogException((object)this, e);
            else
                ShowError((string)null, e.Exception);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                try
                {
                    OnVerifySettings(EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    if (!(ex is ExternalException externalException) || externalException.ErrorCode != -2147217842)
                        ShowError((string)null, ex.Message);
                    e.Cancel = true;
                }
                catch
                {
                    ShowError((string)null, (string)null);
                    e.Cancel = true;
                }
            }
            base.OnFormClosing(e);
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (_translateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
            {
                HelpUtils.TranslateContextHelpMessage((Form)this, ref m);
                DefWndProc(ref m);
            }
            else
                base.WndProc(ref m);
        }

        internal void SetSelectedDataSourceInternal(DataSource value)
        {
            SetSelectedDataSource(value, false);
        }

        internal void SetSelectedDataProviderInternal(DataSource dataSource, DataProvider value)
        {
            SetSelectedDataProvider(dataSource, value, false);
        }

        private void SetSelectedDataSource(DataSource value, bool noSingleItemCheck)
        {
            if (!noSingleItemCheck && _dataSources.Count == 1 && _selectedDataSource != value)
            {
                IEnumerator<DataSource> enumerator = _dataSources.GetEnumerator();
                enumerator.MoveNext();
                if (value != enumerator.Current)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotChangeSingleDataSource);
            }
            if (_selectedDataSource == value)
                return;
            if (value != null)
            {
                _selectedDataSource = _dataSources.Contains(value) ? value : throw new InvalidOperationException(SR.DataConnectionDialog_DataSourceNotFound);
                switch (_selectedDataSource.Providers.Count)
                {
                    case 0:
                        SetSelectedDataProvider(_selectedDataSource, (DataProvider)null, noSingleItemCheck);
                        break;
                    case 1:
                        IEnumerator<DataProvider> enumerator = _selectedDataSource.Providers.GetEnumerator();
                        enumerator.MoveNext();
                        SetSelectedDataProvider(_selectedDataSource, enumerator.Current, true);
                        break;
                    default:
                        DataProvider dataProvider = _selectedDataSource.DefaultProvider;
                        if (_dataProviderSelections.ContainsKey(_selectedDataSource))
                            dataProvider = _dataProviderSelections[_selectedDataSource];
                        SetSelectedDataProvider(_selectedDataSource, dataProvider, noSingleItemCheck);
                        break;
                }
            }
            else
                _selectedDataSource = (DataSource)null;
            if (!_showingDialog)
                return;
            ConfigureDataSourceTextBox();
        }

        private void SetSelectedDataProvider(
          DataSource dataSource,
          DataProvider value,
          bool noSingleItemCheck)
        {
            if (!noSingleItemCheck && dataSource.Providers.Count == 1 && (_dataProviderSelections.ContainsKey(dataSource) && _dataProviderSelections[dataSource] != value || !_dataProviderSelections.ContainsKey(dataSource) && value != null))
            {
                IEnumerator<DataProvider> enumerator = dataSource.Providers.GetEnumerator();
                enumerator.MoveNext();
                if (value != enumerator.Current)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotChangeSingleDataProvider);
            }
            if ((!_dataProviderSelections.ContainsKey(dataSource) || _dataProviderSelections[dataSource] == value) && (_dataProviderSelections.ContainsKey(dataSource) || value == null))
                return;
            if (value != null)
            {
                if (!dataSource.Providers.Contains(value))
                    throw new InvalidOperationException(SR.DataConnectionDialog_DataSourceNoAssociation);
                _dataProviderSelections[dataSource] = value;
            }
            else if (_dataProviderSelections.ContainsKey(dataSource))
                _dataProviderSelections.Remove(dataSource);
            if (!_showingDialog)
                return;
            ConfigureContainerControl();
        }

        private void ConfigureDataSourceTextBox()
        {
            if (SelectedDataSource != null)
            {
                if (SelectedDataSource == UnspecifiedDataSource)
                {
                    if (SelectedDataProvider != null)
                        dataSourceTextBox.Text = SelectedDataProvider.DisplayName;
                    else
                        dataSourceTextBox.Text = (string)null;
                    dataProviderToolTip.SetToolTip((Control)dataSourceTextBox, (string)null);
                }
                else
                {
                    dataSourceTextBox.Text = SelectedDataSource.DisplayName;
                    if (SelectedDataProvider != null)
                    {
                        if (SelectedDataProvider.ShortDisplayName != null)
                        {
                            // ISSUE: reference to a compiler-generated method
                            dataSourceTextBox.Text = SRHelper.DataConnectionDialog_DataSourceWithShortProvider(dataSourceTextBox.Text, SelectedDataProvider.ShortDisplayName);
                        }
                        dataProviderToolTip.SetToolTip((Control)dataSourceTextBox, SelectedDataProvider.DisplayName);
                    }
                    else
                        dataProviderToolTip.SetToolTip((Control)dataSourceTextBox, (string)null);
                }
            }
            else
            {
                dataSourceTextBox.Text = (string)null;
                dataProviderToolTip.SetToolTip((Control)dataSourceTextBox, (string)null);
            }
        }

        private void ConfigureChangeDataSourceButton()
        {
            changeDataSourceButton.Enabled = DataSources.Count > 1 || SelectedDataSource.Providers.Count > 1;
        }

        private void ChangeDataSource(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Ignore;
            Close();
        }

        private void ConfigureContainerControl()
        {
            if (containerControl.Controls.Count == 0)
                _initialContainerControlSize = containerControl.Size;
            if (containerControl.Controls.Count == 0 && ConnectionUIControl != null || containerControl.Controls.Count > 0 && ConnectionUIControl != containerControl.Controls[0])
            {
                containerControl.Controls.Clear();
                if (ConnectionUIControl != null)
                {
                    Size size1 = ConnectionUIControl.PreferredSize;
                    if (size1.Width > 0)
                    {
                        size1 = ConnectionUIControl.PreferredSize;
                        if (size1.Height > 0)
                        {
                            containerControl.Controls.Add((Control)ConnectionUIControl);
                            MinimumSize = Size.Empty;
                            Size size2 = containerControl.Size;
                            containerControl.Size = _initialContainerControlSize;
                            Size preferredSize = ConnectionUIControl.PreferredSize;
                            containerControl.Size = size2;
                            int width1 = _initialContainerControlSize.Width;
                            int width2 = Width;
                            size1 = ClientSize;
                            int width3 = size1.Width;
                            int num1 = width2 - width3;
                            int num2 = width1 - num1 - Padding.Left - containerControl.Margin.Left;
                            Padding padding1 = containerControl.Margin;
                            int right1 = padding1.Right;
                            int num3 = num2 - right1;
                            padding1 = Padding;
                            int right2 = padding1.Right;
                            int val1 = num3 - right2;
                            int num4 = testConnectionButton.Width + testConnectionButton.Margin.Right;
                            Padding margin = buttonsTableLayoutPanel.Margin;
                            int left1 = margin.Left;
                            int num5 = num4 + left1 + buttonsTableLayoutPanel.Width;
                            margin = buttonsTableLayoutPanel.Margin;
                            int right3 = margin.Right;
                            int val2_1 = num5 + right3;
                            int val2_2 = Math.Max(val1, val2_1);
                            preferredSize.Width = Math.Max(preferredSize.Width, val2_2);
                            Size = Size + (preferredSize - containerControl.Size);
                            if (containerControl.Bottom == advancedButton.Top)
                            {
                                ContainerControl containerControl1 = containerControl;
                                margin = containerControl.Margin;
                                int left2 = margin.Left;
                                margin = dataSourceTableLayoutPanel.Margin;
                                int bottom1 = margin.Bottom;
                                margin = containerControl.Margin;
                                int right4 = margin.Right;
                                margin = advancedButton.Margin;
                                int top1 = margin.Top;
                                Padding padding2 = new Padding(left2, bottom1, right4, top1);
                                containerControl1.Margin = padding2;
                                int height1 = Height;
                                margin = containerControl.Margin;
                                int bottom2 = margin.Bottom;
                                margin = advancedButton.Margin;
                                int top2 = margin.Top;
                                int num6 = bottom2 + top2;
                                Height = height1 + num6;
                                ContainerControl containerControl2 = containerControl;
                                int height2 = containerControl2.Height;
                                margin = containerControl.Margin;
                                int bottom3 = margin.Bottom;
                                margin = advancedButton.Margin;
                                int top3 = margin.Top;
                                int num7 = bottom3 + top3;
                                containerControl2.Height = height2 - num7;
                            }
                            Size size3 = SystemInformation.PrimaryMonitorMaximizedWindowSize - SystemInformation.FrameBorderSize - SystemInformation.FrameBorderSize;
                            if (Width > size3.Width)
                            {
                                Width = size3.Width;
                                if (Height + SystemInformation.HorizontalScrollBarHeight <= size3.Height)
                                    Height += SystemInformation.HorizontalScrollBarHeight;
                            }
                            if (Height > size3.Height)
                            {
                                if (Width + SystemInformation.VerticalScrollBarWidth <= size3.Width)
                                    Width += SystemInformation.VerticalScrollBarWidth;
                                Height = size3.Height;
                            }
                            MinimumSize = Size;
                            advancedButton.Enabled = !(ConnectionUIControl is DataConnectionDialog.PropertyGridUIControl);
                            goto label_19;
                        }
                    }
                }
                MinimumSize = Size.Empty;
                if (containerControl.Bottom != advancedButton.Top)
                {
                    ContainerControl containerControl3 = containerControl;
                    int height3 = containerControl3.Height;
                    int bottom4 = containerControl.Margin.Bottom;
                    Padding margin = advancedButton.Margin;
                    int top4 = margin.Top;
                    int num8 = bottom4 + top4;
                    containerControl3.Height = height3 + num8;
                    int height4 = Height;
                    margin = containerControl.Margin;
                    int bottom5 = margin.Bottom;
                    margin = advancedButton.Margin;
                    int top5 = margin.Top;
                    int num9 = bottom5 + top5;
                    Height = height4 - num9;
                    ContainerControl containerControl4 = containerControl;
                    margin = containerControl.Margin;
                    int left = margin.Left;
                    margin = containerControl.Margin;
                    int right = margin.Right;
                    Padding padding = new Padding(left, 0, right, 0);
                    containerControl4.Margin = padding;
                }
                Size = Size - (containerControl.Size - new Size(300, 0));
                MinimumSize = Size;
                advancedButton.Enabled = true;
            }
        label_19:
            if (ConnectionUIControl == null)
                return;
            try
            {
                (ConnectionUIControl as IDataConnectionUIControl).LoadProperties();
            }
            catch
            {
            }
        }

        private void SetConnectionUIControlDockStyle(object sender, EventArgs e)
        {
            if (containerControl.Controls.Count <= 0)
                return;
            DockStyle dockStyle = DockStyle.None;
            Size size = containerControl.Size;
            Size minimumSize = containerControl.Controls[0].MinimumSize;
            if (size.Width >= minimumSize.Width && size.Height >= minimumSize.Height)
                dockStyle = DockStyle.Fill;
            if (size.Width - SystemInformation.VerticalScrollBarWidth >= minimumSize.Width && size.Height < minimumSize.Height)
                dockStyle = DockStyle.Top;
            if (size.Width < minimumSize.Width && size.Height - SystemInformation.HorizontalScrollBarHeight >= minimumSize.Height)
                dockStyle = DockStyle.Left;
            containerControl.Controls[0].Dock = dockStyle;
        }

        private void ShowAdvanced(object sender, EventArgs e)
        {
            DataConnectionAdvancedDialog connectionAdvancedDialog = new DataConnectionAdvancedDialog(ConnectionProperties, this);
            DialogResult dialogResult = DialogResult.None;
            try
            {
                if (Container != null)
                    Container.Add((IComponent)connectionAdvancedDialog);
                dialogResult = connectionAdvancedDialog.ShowDialog((IWin32Window)this);
            }
            finally
            {
                if (Container != null)
                    Container.Remove((IComponent)connectionAdvancedDialog);
                connectionAdvancedDialog.Dispose();
            }
            if (dialogResult != DialogResult.OK)
                return;
            if (ConnectionUIControl == null)
                return;
            try
            {
                (ConnectionUIControl as IDataConnectionUIControl).LoadProperties();
            }
            catch
            {
            }
            ConfigureAcceptButton((object)this, EventArgs.Empty);
        }

        private void TestConnection(object sender, EventArgs e)
        {
            Cursor current = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                ConnectionProperties.Test();
            }
            catch (Exception ex)
            {
                Cursor.Current = current;
                ShowError(SR.DataConnectionDialog_TestResults, ex);
                return;
            }
            catch
            {
                Cursor.Current = current;
                ShowError(SR.DataConnectionDialog_TestResults, (string)null);
                return;
            }
            Cursor.Current = current;
            ShowMessage(SR.DataConnectionDialog_TestResults, SR.DataConnectionDialog_TestConnectionSucceeded);
        }

        private void ConfigureAcceptButton(object sender, EventArgs e)
        {
            try
            {
                acceptButton.Enabled = ConnectionProperties != null && ConnectionProperties.IsComplete;
            }
            catch
            {
                acceptButton.Enabled = true;
            }
        }

        private void HandleAccept(object sender, EventArgs e) => acceptButton.Focus();

        private void PaintSeparator(object sender, PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Pen pen1 = new Pen(ControlPaint.Dark(BackColor, 0.0f));
            Pen pen2 = new Pen(ControlPaint.Light(BackColor, 1f));
            int width = separatorPanel.Width;
            graphics.DrawLine(pen1, 0, 0, width, 0);
            graphics.DrawLine(pen2, 0, 1, width, 1);
        }

        private void HandleDialogException(object sender, ThreadExceptionEventArgs e)
        {
            OnDialogException(e);
        }

        private void ShowMessage(string title, string message)
        {
            if (GetService(typeof(IUIService)) is IUIService service)
            {
                service.ShowMessage(message);
            }
            else
            {
                int num = (int)RTLAwareMessageBox.Show(title, message, MessageBoxIcon.Asterisk);
            }
        }

        private void ShowError(string title, Exception ex)
        {
            if (GetService(typeof(IUIService)) is IUIService service)
            {
                service.ShowError(ex);
            }
            else
            {
                int num = (int)RTLAwareMessageBox.Show(title, ex.Message, MessageBoxIcon.Exclamation);
            }
        }

        private void ShowError(string title, string message)
        {
            if (GetService(typeof(IUIService)) is IUIService service)
            {
                service.ShowError(message);
            }
            else
            {
                int num = (int)RTLAwareMessageBox.Show(title, message, MessageBoxIcon.Exclamation);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = (IContainer)new System.ComponentModel.Container();
            ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(DataConnectionDialog));
            dataSourceLabel = new Label();
            dataSourceTableLayoutPanel = new TableLayoutPanel();
            dataSourceTextBox = new TextBox();
            changeDataSourceButton = new Button();
            containerControl = new ContainerControl();
            advancedButton = new Button();
            separatorPanel = new Panel();
            testConnectionButton = new Button();
            buttonsTableLayoutPanel = new TableLayoutPanel();
            acceptButton = new Button();
            cancelButton = new Button();
            dataProviderToolTip = new ToolTip(components);
            dataSourceTableLayoutPanel.SuspendLayout();
            buttonsTableLayoutPanel.SuspendLayout();
            SuspendLayout();
            componentResourceManager.ApplyResources((object)dataSourceLabel, "dataSourceLabel");
            dataSourceLabel.FlatStyle = FlatStyle.System;
            dataSourceLabel.Name = "dataSourceLabel";
            componentResourceManager.ApplyResources((object)dataSourceTableLayoutPanel, "dataSourceTableLayoutPanel");
            dataSourceTableLayoutPanel.Controls.Add((Control)dataSourceTextBox, 0, 0);
            dataSourceTableLayoutPanel.Controls.Add((Control)changeDataSourceButton, 1, 0);
            dataSourceTableLayoutPanel.Name = "dataSourceTableLayoutPanel";
            componentResourceManager.ApplyResources((object)dataSourceTextBox, "dataSourceTextBox");
            dataSourceTextBox.Name = "dataSourceTextBox";
            dataSourceTextBox.ReadOnly = true;
            componentResourceManager.ApplyResources((object)changeDataSourceButton, "changeDataSourceButton");
            changeDataSourceButton.MinimumSize = new Size(75, 23);
            changeDataSourceButton.Name = "changeDataSourceButton";
            changeDataSourceButton.Click += new EventHandler(ChangeDataSource);
            componentResourceManager.ApplyResources((object)containerControl, "containerControl");
            containerControl.Name = "containerControl";
            containerControl.SizeChanged += new EventHandler(SetConnectionUIControlDockStyle);
            componentResourceManager.ApplyResources((object)advancedButton, "advancedButton");
            advancedButton.MinimumSize = new Size(81, 23);
            advancedButton.Name = "advancedButton";
            advancedButton.Click += new EventHandler(ShowAdvanced);
            componentResourceManager.ApplyResources((object)separatorPanel, "separatorPanel");
            separatorPanel.Name = "separatorPanel";
            separatorPanel.Paint += new PaintEventHandler(PaintSeparator);
            componentResourceManager.ApplyResources((object)testConnectionButton, "testConnectionButton");
            testConnectionButton.MinimumSize = new Size(101, 23);
            testConnectionButton.Name = "testConnectionButton";
            testConnectionButton.Click += new EventHandler(TestConnection);
            componentResourceManager.ApplyResources((object)buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
            buttonsTableLayoutPanel.Controls.Add((Control)acceptButton, 0, 0);
            buttonsTableLayoutPanel.Controls.Add((Control)cancelButton, 1, 0);
            buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
            componentResourceManager.ApplyResources((object)acceptButton, "acceptButton");
            acceptButton.DialogResult = DialogResult.OK;
            acceptButton.MinimumSize = new Size(75, 23);
            acceptButton.Name = "acceptButton";
            acceptButton.Click += new EventHandler(HandleAccept);
            componentResourceManager.ApplyResources((object)cancelButton, "cancelButton");
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.MinimumSize = new Size(75, 23);
            cancelButton.Name = "cancelButton";
            AcceptButton = (IButtonControl)acceptButton;
            componentResourceManager.ApplyResources((object)this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = (IButtonControl)cancelButton;
            Controls.Add((Control)buttonsTableLayoutPanel);
            Controls.Add((Control)testConnectionButton);
            Controls.Add((Control)separatorPanel);
            Controls.Add((Control)advancedButton);
            Controls.Add((Control)containerControl);
            Controls.Add((Control)dataSourceTableLayoutPanel);
            Controls.Add((Control)dataSourceLabel);
            HelpButton = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = nameof(DataConnectionDialog);
            ShowIcon = false;
            ShowInTaskbar = false;
            dataSourceTableLayoutPanel.ResumeLayout(false);
            dataSourceTableLayoutPanel.PerformLayout();
            buttonsTableLayoutPanel.ResumeLayout(false);
            buttonsTableLayoutPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private class DataSourceCollection :
          ICollection<DataSource>,
          IEnumerable<DataSource>,
          IEnumerable
        {
            private List<DataSource> _list = new List<DataSource>();
            private DataConnectionDialog _dialog;

            public DataSourceCollection(DataConnectionDialog dialog) => _dialog = dialog;

            public int Count => _list.Count;

            public bool IsReadOnly => _dialog._showingDialog;

            public void Add(DataSource item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                if (_dialog._showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                if (_list.Contains(item))
                    return;
                _list.Add(item);
            }

            public bool Contains(DataSource item) => _list.Contains(item);

            public bool Remove(DataSource item)
            {
                if (_dialog._showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                int num = _list.Remove(item) ? 1 : 0;
                if (item != _dialog.SelectedDataSource)
                    return num != 0;
                _dialog.SetSelectedDataSource((DataSource)null, true);
                return num != 0;
            }

            public void Clear()
            {
                if (_dialog._showingDialog)
                    throw new InvalidOperationException(SR.DataConnectionDialog_CannotModifyState);
                _list.Clear();
                _dialog.SetSelectedDataSource((DataSource)null, true);
            }

            public void CopyTo(DataSource[] array, int arrayIndex)
            {
                _list.CopyTo(array, arrayIndex);
            }

            public IEnumerator<DataSource> GetEnumerator()
            {
                return (IEnumerator<DataSource>)_list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_list.GetEnumerator();
        }

        private class PropertyGridUIControl : UserControl, IDataConnectionUIControl
        {
            private IDataConnectionProperties connectionProperties;
            private DataConnectionAdvancedDialog.SpecializedPropertyGrid propertyGrid;

            public PropertyGridUIControl()
            {
                propertyGrid = new DataConnectionAdvancedDialog.SpecializedPropertyGrid();
                SuspendLayout();
                propertyGrid.CommandsVisibleIfAvailable = true;
                propertyGrid.Dock = DockStyle.Fill;
                propertyGrid.Location = Point.Empty;
                propertyGrid.Margin = new Padding(0);
                propertyGrid.Name = nameof(propertyGrid);
                propertyGrid.TabIndex = 0;
                Controls.Add((Control)propertyGrid);
                Name = nameof(PropertyGridUIControl);
                ResumeLayout(false);
                PerformLayout();
            }

            public void Initialize(IDataConnectionProperties dataConnectionProperties)
            {
                connectionProperties = dataConnectionProperties;
            }

            public void LoadProperties()
            {
                propertyGrid.SelectedObject = (object)connectionProperties;
            }

            public override Size GetPreferredSize(Size proposedSize)
            {
                return propertyGrid.GetPreferredSize(proposedSize);
            }
        }

        private class BasicConnectionProperties : IDataConnectionProperties
        {
            private string _s;

            public void Reset() => _s = string.Empty;

            public void Parse(string s)
            {
                _s = s;
                if (PropertyChanged == null)
                    return;
                PropertyChanged((object)this, EventArgs.Empty);
            }

            [Browsable(false)]
            public bool IsExtensible => false;

            public void Add(string propertyName) => throw new NotImplementedException();

            public bool Contains(string propertyName) => propertyName == "ConnectionString";

            public object this[string propertyName]
            {
                get => propertyName == "ConnectionString" ? (object)ConnectionString : (object)null;
                set
                {
                    if (!(propertyName == "ConnectionString"))
                        return;
                    ConnectionString = value as string;
                }
            }

            public void Remove(string propertyName) => throw new NotImplementedException();

            public event EventHandler PropertyChanged;

            public void Reset(string propertyName) => _s = string.Empty;

            [Browsable(false)]
            public bool IsComplete => true;

            public string ConnectionString
            {
                get => ToFullString();
                set => Parse(value);
            }

            public void Test()
            {
            }

            public string ToFullString() => _s;

            public string ToDisplayString() => _s;
        }
    }
}
