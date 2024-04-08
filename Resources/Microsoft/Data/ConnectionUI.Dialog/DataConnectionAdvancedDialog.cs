using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using System.Windows.Forms.ComponentModel.Com2Interop;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	internal class DataConnectionAdvancedDialog : Form
	{
		private string _savedConnectionString;
		private DataConnectionDialog _mainDialog;
		private IContainer components;
		private TextBox textBox;
		private TableLayoutPanel buttonsTableLayoutPanel;
		private Button okButton;
		private Button cancelButton;
		private DataConnectionAdvancedDialog.SpecializedPropertyGrid propertyGrid;

		public DataConnectionAdvancedDialog()
		{
			InitializeComponent();
			if (components == null)
				components = (IContainer)new System.ComponentModel.Container();
			components.Add((IComponent)new UserPreferenceChangedHandler((Form)this));
		}

		public DataConnectionAdvancedDialog(
		  IDataConnectionProperties connectionProperties,
		  DataConnectionDialog mainDialog)
		  : this()
		{
			_savedConnectionString = connectionProperties.ToFullString();
			propertyGrid.SelectedObject = (object)connectionProperties;
			_mainDialog = mainDialog;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			ConfigureTextBox();
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			propertyGrid.Focus();
		}

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			textBox.Width = propertyGrid.Width;
		}

		protected override void OnHelpRequested(HelpEventArgs hevent)
		{
			Control control = (Control)this;
			while (control is ContainerControl containerControl && containerControl != propertyGrid && containerControl.ActiveControl != null)
				control = containerControl.ActiveControl;
			DataConnectionDialogContext context = DataConnectionDialogContext.Advanced;
			if (control == propertyGrid)
				context = DataConnectionDialogContext.AdvancedPropertyGrid;
			if (control == textBox)
				context = DataConnectionDialogContext.AdvancedTextBox;
			if (control == okButton)
				context = DataConnectionDialogContext.AdvancedOkButton;
			if (control == cancelButton)
				context = DataConnectionDialogContext.AdvancedCancelButton;
			ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
			_mainDialog.OnContextHelpRequested(e);
			hevent.Handled = e.Handled;
			if (e.Handled)
				return;
			base.OnHelpRequested(hevent);
		}

		protected override void WndProc(ref Message m)
		{
			if (_mainDialog.TranslateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
				HelpUtils.TranslateContextHelpMessage((Form)this, ref m);
			base.WndProc(ref m);
		}

		private void SetTextBox(object s, PropertyValueChangedEventArgs e) => ConfigureTextBox();

		private void ConfigureTextBox()
		{
			if (propertyGrid.SelectedObject is IDataConnectionProperties)
			{
				try
				{
					textBox.Text = (propertyGrid.SelectedObject as IDataConnectionProperties).ToDisplayString();
				}
				catch
				{
					textBox.Text = (string)null;
				}
			}
			else
				textBox.Text = (string)null;
		}

		private void RevertProperties(object sender, EventArgs e)
		{
			try
			{
				(propertyGrid.SelectedObject as IDataConnectionProperties).Parse(_savedConnectionString);
			}
			catch
			{
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
			ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(DataConnectionAdvancedDialog));
			propertyGrid = new DataConnectionAdvancedDialog.SpecializedPropertyGrid();
			textBox = new TextBox();
			buttonsTableLayoutPanel = new TableLayoutPanel();
			okButton = new Button();
			cancelButton = new Button();
			buttonsTableLayoutPanel.SuspendLayout();
			SuspendLayout();
			componentResourceManager.ApplyResources((object)propertyGrid, "propertyGrid");
			propertyGrid.CommandsActiveLinkColor = SystemColors.ActiveCaption;
			propertyGrid.CommandsDisabledLinkColor = SystemColors.ControlDark;
			propertyGrid.CommandsLinkColor = SystemColors.ActiveCaption;
			propertyGrid.MinimumSize = new Size(270, 250);
			propertyGrid.Name = "propertyGrid";
			propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler(SetTextBox);
			componentResourceManager.ApplyResources((object)textBox, "textBox");
			textBox.Name = "textBox";
			textBox.ReadOnly = true;
			componentResourceManager.ApplyResources((object)buttonsTableLayoutPanel, "buttonsTableLayoutPanel");
			buttonsTableLayoutPanel.Controls.Add((Control)okButton, 0, 0);
			buttonsTableLayoutPanel.Controls.Add((Control)cancelButton, 1, 0);
			buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
			componentResourceManager.ApplyResources((object)okButton, "okButton");
			okButton.DialogResult = DialogResult.OK;
			okButton.MinimumSize = new Size(75, 23);
			okButton.Name = "okButton";
			componentResourceManager.ApplyResources((object)cancelButton, "cancelButton");
			cancelButton.DialogResult = DialogResult.Cancel;
			cancelButton.MinimumSize = new Size(75, 23);
			cancelButton.Name = "cancelButton";
			cancelButton.Click += new EventHandler(RevertProperties);
			AcceptButton = (IButtonControl)okButton;
			componentResourceManager.ApplyResources((object)this, "$this");
			AutoScaleMode = AutoScaleMode.Font;
			CancelButton = (IButtonControl)cancelButton;
			Controls.Add((Control)buttonsTableLayoutPanel);
			Controls.Add((Control)textBox);
			Controls.Add((Control)propertyGrid);
			HelpButton = true;
			MaximizeBox = false;
			MinimizeBox = false;
			Name = nameof(DataConnectionAdvancedDialog);
			ShowIcon = false;
			ShowInTaskbar = false;
			buttonsTableLayoutPanel.ResumeLayout(false);
			buttonsTableLayoutPanel.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}

		internal class SpecializedPropertyGrid : PropertyGrid
		{
			private ContextMenuStrip _contextMenu;

			public SpecializedPropertyGrid()
			{
				_contextMenu = new ContextMenuStrip();
				_contextMenu.Items.AddRange(new ToolStripItem[6]
				{
		  (ToolStripItem) new ToolStripMenuItem(),
		  (ToolStripItem) new ToolStripSeparator(),
		  (ToolStripItem) new ToolStripMenuItem(),
		  (ToolStripItem) new ToolStripMenuItem(),
		  (ToolStripItem) new ToolStripSeparator(),
		  (ToolStripItem) new ToolStripMenuItem()
				});
				_contextMenu.Items[0].Text = SR.GetString("DataConnectionAdvancedDialog_Reset");
				_contextMenu.Items[0].Click += new EventHandler(ResetProperty);
				_contextMenu.Items[2].Text = SR.GetString("DataConnectionAdvancedDialog_Add");
				_contextMenu.Items[2].Click += new EventHandler(AddProperty);
				_contextMenu.Items[3].Text = SR.GetString("DataConnectionAdvancedDialog_Remove");
				_contextMenu.Items[3].Click += new EventHandler(RemoveProperty);
				_contextMenu.Items[5].Text = SR.GetString("DataConnectionAdvancedDialog_Description");
				_contextMenu.Items[5].Click += new EventHandler(ToggleDescription);
				(_contextMenu.Items[5] as ToolStripMenuItem).Checked = HelpVisible;
				_contextMenu.Opened += new EventHandler(SetupContextMenu);
				ContextMenuStrip = _contextMenu;
				DrawFlatToolbar = true;
				Size = new Size(270, 250);
				MinimumSize = Size;
			}

			protected override void OnHandleCreated(EventArgs e)
			{
				ProfessionalColorTable service = ParentForm == null || ParentForm.Site == null ? (ProfessionalColorTable)null : ParentForm.Site.GetService(typeof(ProfessionalColorTable)) as ProfessionalColorTable;
				if (service != null)
					ToolStripRenderer = (ToolStripRenderer)new ToolStripProfessionalRenderer(service);
				base.OnHandleCreated(e);
			}

			protected override void OnFontChanged(EventArgs e)
			{
				base.OnFontChanged(e);
				LargeButtons = (double)Font.SizeInPoints >= 15.0;
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == 7)
				{
					Focus();
					((IComPropertyBrowser)this).HandleF4();
				}
				base.WndProc(ref m);
			}

			private void SetupContextMenu(object sender, EventArgs e)
			{
				_contextMenu.Items[0].Enabled = SelectedGridItem.GridItemType == GridItemType.Property;
				if (_contextMenu.Items[0].Enabled && SelectedGridItem.PropertyDescriptor != null)
				{
					object component = SelectedObject;
					if (SelectedObject is ICustomTypeDescriptor)
						component = (SelectedObject as ICustomTypeDescriptor).GetPropertyOwner(SelectedGridItem.PropertyDescriptor);
					_contextMenu.Items[0].Enabled = _contextMenu.Items[3].Enabled = SelectedGridItem.PropertyDescriptor.CanResetValue(component);
				}
				_contextMenu.Items[2].Visible = _contextMenu.Items[3].Visible = (SelectedObject as IDataConnectionProperties).IsExtensible;
				if (_contextMenu.Items[3].Visible)
				{
					_contextMenu.Items[3].Enabled = SelectedGridItem.GridItemType == GridItemType.Property;
					if (_contextMenu.Items[3].Enabled && SelectedGridItem.PropertyDescriptor != null)
						_contextMenu.Items[3].Enabled = !SelectedGridItem.PropertyDescriptor.IsReadOnly;
				}
				_contextMenu.Items[1].Visible = _contextMenu.Items[2].Visible || _contextMenu.Items[3].Visible;
			}

			private void ResetProperty(object sender, EventArgs e)
			{
				object oldValue = SelectedGridItem.Value;
				object component = SelectedObject;
				if (SelectedObject is ICustomTypeDescriptor)
					component = (SelectedObject as ICustomTypeDescriptor).GetPropertyOwner(SelectedGridItem.PropertyDescriptor);
				SelectedGridItem.PropertyDescriptor.ResetValue(component);
				Refresh();
				OnPropertyValueChanged(new PropertyValueChangedEventArgs(SelectedGridItem, oldValue));
			}

			private void AddProperty(object sender, EventArgs e)
			{
				if (!(ParentForm is DataConnectionDialog mainDialog))
					mainDialog = (ParentForm as DataConnectionAdvancedDialog)._mainDialog;
				AddPropertyDialog addPropertyDialog = new AddPropertyDialog(mainDialog);
				try
				{
					if (ParentForm.Container != null)
						ParentForm.Container.Add((IComponent)addPropertyDialog);
					if (addPropertyDialog.ShowDialog((IWin32Window)ParentForm) != DialogResult.OK)
						return;
					(SelectedObject as IDataConnectionProperties).Add(addPropertyDialog.PropertyName);
					Refresh();
					GridItem currentItem = SelectedGridItem;
					while (currentItem.Parent != null)
						currentItem = currentItem.Parent;
					GridItem gridItem = LocateGridItem(currentItem, addPropertyDialog.PropertyName);
					if (gridItem == null)
						return;
					SelectedGridItem = gridItem;
				}
				finally
				{
					if (ParentForm.Container != null)
						ParentForm.Container.Remove((IComponent)addPropertyDialog);
					addPropertyDialog.Dispose();
				}
			}

			private void RemoveProperty(object sender, EventArgs e)
			{
				(SelectedObject as IDataConnectionProperties).Remove(SelectedGridItem.Label);
				Refresh();
				OnPropertyValueChanged(new PropertyValueChangedEventArgs((GridItem)null, (object)null));
			}

			private void ToggleDescription(object sender, EventArgs e)
			{
				HelpVisible = !HelpVisible;
				(_contextMenu.Items[5] as ToolStripMenuItem).Checked = !(_contextMenu.Items[5] as ToolStripMenuItem).Checked;
			}

			private GridItem LocateGridItem(GridItem currentItem, string propertyName)
			{
				if (currentItem.GridItemType == GridItemType.Property && currentItem.Label.Equals(propertyName, StringComparison.CurrentCulture))
					return currentItem;
				GridItem gridItem1 = (GridItem)null;
				foreach (GridItem gridItem2 in currentItem.GridItems)
				{
					gridItem1 = LocateGridItem(gridItem2, propertyName);
					if (gridItem1 != null)
						break;
				}
				return gridItem1;
			}
		}
	}
}
