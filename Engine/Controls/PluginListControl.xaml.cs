﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.Generic;
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

		public IList<PluginItem> GetAllMetods()
		{
			var methods = Global.AppSettings.Plugins
				.Where(x => x.Mi.DeclaringType.FullName == ClassFullName)
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
				var summary = XmlDocHelper.GetSummaryText(first.Mi.DeclaringType, FormatText.RemoveIdentAndTrimSpaces);
				ClassDescription = summary;
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

		private void EnableAllButton_Click(object sender, RoutedEventArgs e)
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				method.IsEnabled = true;
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
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				if (method.RiskLevel >= RiskLevel.None && method.RiskLevel <= RiskLevel.Low)
					method.IsEnabled = true;
		}

		private void EnableMediumRiskButton_Click(object sender, RoutedEventArgs e)
		{
			var methods = GetAllMetods();
			foreach (var method in methods)
				if (method.RiskLevel >= RiskLevel.None && method.RiskLevel <= RiskLevel.Medium)
					method.IsEnabled = true;
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
