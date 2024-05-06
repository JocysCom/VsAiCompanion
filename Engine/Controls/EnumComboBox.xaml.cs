using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System;
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
		public class CheckBoxViewModel : NotifyPropertyChanged
		{
			public string Description { get => _Description; set => SetProperty(ref _Description, value); }
			private string _Description;
			public bool IsChecked { get => _IsChecked; set => SetProperty(ref _IsChecked, value); }
			private bool _IsChecked;

			public Enum Value { get; set; }

			public Visibility CheckVisibility { get; set; }

		}

		public EnumComboBox()
		{
			InitializeComponent();
		}

		private void List_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.PropertyDescriptor?.Name == nameof(CheckBoxViewModel.IsChecked))
			{
				var data = (BindingList<CheckBoxViewModel>)sender;
				UpdateTopDescription(data);
				var top = data[0];
				var value = data
					.Where(x => x.IsChecked)
					.Aggregate(0L, (current, item) => Convert.ToInt64(current) | Convert.ToInt64(item.Value));
				var newValue = (Enum)Enum.ToObject(top.Value.GetType(), value);
				if (!Equals(SelectedValue, newValue))
					SelectedValue = newValue;
			}
		}

		public void UpdateTopDescription(BindingList<CheckBoxViewModel> data)
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

		public static BindingList<CheckBoxViewModel> GetItemSource<T>() where T : Enum
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
			var list = new BindingList<CheckBoxViewModel>();
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
			var value = (Enum)box.GetValue(SelectedValueProperty);
			var items = (BindingList<CheckBoxViewModel>)box.ItemsSource;
			foreach (var item in items)
			{
				var isChecked = !IsDefault(item.Value) && value.HasFlag(item.Value);
				if (item.IsChecked != isChecked)
					item.IsChecked = isChecked;
			}
		}

		static bool IsDefault(Enum value)
			=> Convert.ToInt64(value) == 0L;

		#endregion

		void UpdateListMonitoring()
		{
			var data = (BindingList<CheckBoxViewModel>)ItemsSource;
			data.ListChanged += List_ListChanged;
			SelectedIndex = 0;
		}

		private void ComboBox_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
				UpdateListMonitoring();

		}
	}
}
