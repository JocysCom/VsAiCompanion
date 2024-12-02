using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	public partial class EnumComboBox : ComboBox
	{

		public EnumComboBox()
		{
			InitializeComponent();
		}

		private void Data_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
				foreach (INotifyPropertyChanged item in e.OldItems)
					item.PropertyChanged -= Data_Item_PropertyChangedEventArgs;
			if (e.NewItems != null)
				foreach (INotifyPropertyChanged item in e.NewItems)
					item.PropertyChanged += Data_Item_PropertyChangedEventArgs;
		}

		void Data_Item_PropertyChangedEventArgs(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CheckBoxViewModel.IsChecked))
				UpdateSelectedValue();
		}

		public void UpdateSelectedValue()
		{
			var data = (ObservableCollection<CheckBoxViewModel>)ItemsSource;
			UpdateTopDescription(data);
			var top = data[0];
			var value = data
				.Where(x => x.IsChecked)
				.Aggregate(0L, (current, item) => Convert.ToInt64(current) | Convert.ToInt64(item.Value));
			var newValue = (Enum)Enum.ToObject(top.Value.GetType(), value);
			if (!Equals(SelectedValue, newValue))
				SelectedValue = newValue;
		}

		public void UpdateTopDescription(ObservableCollection<CheckBoxViewModel> data)
		{
			var top = data[0];
			var choice = data.Skip(1);
			var count = choice.Count(x => x.IsChecked);
			var names = choice.Where(x => x.IsChecked).Select(x => x.Description).ToList();
			if (count == 0)
				top.Description = This.Text = "None";
			else if (count == 1)
				top.Description = This.Text = names.First();
			else
				top.Description = This.Text = $"{names.First()} + " + (count - 1).ToString();
		}

		public static ObservableCollection<CheckBoxViewModel> GetItemSource<T>() where T : Enum
		{
			var items = Enum.GetValues(typeof(T))
				.Cast<T>()
				.OrderBy(x => GetOrder(x))
				.Select((e, i) => new CheckBoxViewModel
				{
					CheckVisibility = i == 0 ? Visibility.Collapsed : Visibility.Visible,
					Description = i == 0 ? "None" : GetDescription(e),
					Value = e
				})
				.ToList();
			var list = new ObservableCollection<CheckBoxViewModel>();
			foreach (var item in items)
				list.Add(item);
			return list;
		}

		public static string GetDescription(Enum value)
		{
			var field = value.GetType().GetField(value.ToString());
			var attribute = field.GetCustomAttribute<DescriptionAttribute>();
			return attribute?.Description ?? value.ToString();

		}

		public static int GetOrder(Enum value)
		{
			var field = value.GetType().GetField(value.ToString());
			var attribute = field.GetCustomAttribute<JsonPropertyOrderAttribute>();
			return attribute?.Order ?? int.MaxValue;
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SelectedIndex != 0)
				SelectedIndex = 0;
		}

		#region Binding

		private static readonly new DependencyProperty SelectedValueProperty =
			DependencyProperty.Register("SelectedValue", typeof(object), typeof(EnumComboBox),
		new FrameworkPropertyMetadata(default(object), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

		public new object SelectedValue
		{
			get => GetValue(SelectedValueProperty);
			set => SetValue(SelectedValueProperty, value);
		}

		private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var box = (EnumComboBox)d;
			if (ControlsHelper.IsDesignMode(box))
				return;
			var value = (Enum)box.GetValue(SelectedValueProperty);
			var items = ((ObservableCollection<CheckBoxViewModel>)box.ItemsSource)?.ToArray();
			if (items == null)
				return;
			foreach (var item in items)
			{
				if (item == null)
					continue;
				var isChecked = !IsDefault(item.Value) && (value?.HasFlag(item.Value) ?? false);
				if (item.IsChecked != isChecked)
					item.IsChecked = isChecked;
			}
		}

		static bool IsDefault(Enum value)
			=> Convert.ToInt64(value) == 0L;

		#endregion

		void UpdateListMonitoring()
		{
			var data = (ObservableCollection<CheckBoxViewModel>)ItemsSource;
			data.CollectionChanged += Data_CollectionChanged;
			foreach (INotifyPropertyChanged item in data)
				item.PropertyChanged += Data_Item_PropertyChangedEventArgs;
			SelectedIndex = 0;
			UpdateSelectedValue();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				UpdateListMonitoring();
				AppHelper.InitHelp(this);
				//UiPresetsManager.InitControl(this, true);
				UiPresetsManager.AddControls(this);
			}
		}
	}
}
