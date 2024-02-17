using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginListControl.xaml
	/// </summary>
	public partial class PluginListControl : UserControl
	{
		public PluginListControl()
		{
			InitializeComponent();
			var list = new SortableBindingList<PluginItem>();
			CurrentItems = list;
			MainItemsControl.ItemsSource = CurrentItems;
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.Plugins.ListChanged += Plugins_ListChanged;
			UpdateOnListChanged();

		}
		private async void Plugins_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnListChanged, AppHelper.NavigateDelayMs);
		}

		public void UpdateOnListChanged()
		{
			var methods = Global.AppSettings.Plugins
				.Where(x => x.Mi.DeclaringType.FullName == ClassFullName)
				.ToList();
			ClassLibrary.Collections.CollectionsHelper.Synchronize(methods, CurrentItems, new PluginItemComparer());
		}

		class PluginItemComparer : IEqualityComparer<PluginItem>
		{
			public bool Equals(PluginItem x, PluginItem y) => x.Id == y.Id;
			public int GetHashCode(PluginItem obj) => throw new System.NotImplementedException();
		}

		#region Properties

		public static readonly DependencyProperty ClassFullNameProperty =
		DependencyProperty.Register(nameof(ClassFullName), typeof(string), typeof(PluginListControl),
		new FrameworkPropertyMetadata((string)"", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnClassFullNameChanged));

		[DefaultValue(typeof(string), "")]
		public string ClassFullName
		{
			get => (string)GetValue(ClassFullNameProperty);
			set => SetValue(ClassFullNameProperty, value);
		}

		#endregion

		private async static void OnClassFullNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as PluginListControl;
			if (control != null)
			{
				// e.NewValue contains the new value of the property
				// e.OldValue contains the old value of the property
				await Helper.Delay(control.UpdateOnListChanged, AppHelper.NavigateDelayMs);
			}
		}

		public SortableBindingList<PluginItem> CurrentItems { get; set; }

	}
}
