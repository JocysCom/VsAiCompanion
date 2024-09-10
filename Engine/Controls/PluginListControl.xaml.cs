using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginListControl.xaml
	/// </summary>
	public partial class PluginListControl : UserControl, INotifyPropertyChanged
	{
		public PluginListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.Plugins.ListChanged += Plugins_ListChanged;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			Global.UserProfile.PropertyChanged += profile_PropertyChanged;
		}


		public ObservableCollection<PluginItem> CurrentItems { get; set; } = new ObservableCollection<PluginItem>();

		private async void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.MaxRiskLevel) ||
				e.PropertyName == nameof(AppData.MaxRiskLevelWhenSignedOut)
				)
			{
				var view = (ICollectionView)MainItemsControl.ItemsSource;
				await Helper.Delay(view.Refresh);
			}
		}

		private async void profile_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (
				e.PropertyName == nameof(UserProfile.IsSignedIn) ||
				e.PropertyName == nameof(UserProfile.UserGroups))
			{
				var view = (ICollectionView)MainItemsControl.ItemsSource;
				await Helper.Delay(view.Refresh);
			}
		}

		private async void Plugins_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnListChanged, AppHelper.NavigateDelayMs);
		}

		public IList<PluginItem> GetAllMetods()
		{
			var methods = Global.AppSettings.Plugins
				.Where(x => x.Class == CategoryName)
				.ToList();
			return methods;
		}

		public void UpdateOnListChanged()
		{
			var methods = GetAllMetods();
			methods = methods
				.OrderBy(x => x.RiskLevel)
				.ThenBy(x => x.Name)
				.ToArray();
			var first = methods.FirstOrDefault();
			if (first != null)
			{
				ClassDescription = first.Mi is null
					? ""
					: XmlDocHelper.GetSummaryText(first.Mi.DeclaringType, FormatText.RemoveIdentAndTrimSpaces);
				OnPropertyChanged(nameof(ClassDescription));
			}
			ClassLibrary.Collections.CollectionsHelper.Synchronize(methods, CurrentItems, new PluginItemComparer());
		}

		public string ClassDescription { get; set; }

		class PluginItemComparer : IEqualityComparer<PluginItem>
		{
			public bool Equals(PluginItem x, PluginItem y) => x.Id == y.Id;
			public int GetHashCode(PluginItem obj) => throw new System.NotImplementedException();
		}

		#region Properties

		public static readonly DependencyProperty CategoryNameProperty =
		DependencyProperty.Register(nameof(CategoryName), typeof(string), typeof(PluginListControl),
		new FrameworkPropertyMetadata((string)"", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnClassFullNameChanged));

		[DefaultValue(typeof(string), "")]
		public string CategoryName
		{
			get => (string)GetValue(CategoryNameProperty);
			set => SetValue(CategoryNameProperty, value);
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

		private void DisableAllButton_Click(object sender, RoutedEventArgs e)
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				method.IsEnabled = false;
		}

		private void ResetToDefault_Click(object sender, RoutedEventArgs e)
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				method.IsEnabled = method.RiskLevel >= RiskLevel.None && method.RiskLevel <= RiskLevel.Low;
		}

		private void EnableLowRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Low);

		private void EnableMediumRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Medium);

		private void EnableHighRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.High);

		private void EnableAllButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Critical);

		void EnableUpTo(RiskLevel maxRiskLevel)
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				if (method.RiskLevel >= RiskLevel.None && method.RiskLevel <= maxRiskLevel)
					method.IsEnabled = true;
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				var item = (PluginCategory)DataContext;
				Name = $"{item.Name}Panel";
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				//UiPresetsManager.AddControls(this);
			}
		}

		#region Copy and Save

		System.Windows.Forms.SaveFileDialog ExportSaveFileDialog { get; } = new System.Windows.Forms.SaveFileDialog();

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			var completionTools = PluginsManager.GetCompletionTools(RiskLevel.Critical);
			var text = Client.Serialize(completionTools, true);
			AppHelper.SetClipboard(text);
		}

		private void SaveAsButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = ExportSaveFileDialog;
			dialog.DefaultExt = "*.json";
			dialog.FileName = $"{JocysCom.ClassLibrary.Configuration.AssemblyInfo.Entry.Product} Completion Tools.json".Replace(" ", "_");
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Save As Tool Function File";
			DialogHelper.AddFilter(dialog, ".json");
			DialogHelper.AddFilter(dialog);
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			// Cast the document to an JSON Document
			var completionTools = PluginsManager.GetCompletionTools(RiskLevel.Critical);
			var text = Client.Serialize(completionTools, true);
			if (string.IsNullOrEmpty(text))
				return;
			var bytes = System.Text.Encoding.UTF8.GetBytes(text);
			JocysCom.ClassLibrary.Configuration.SettingsHelper.WriteIfDifferent(dialog.FileName, bytes);
		}

		#endregion
	}
}
