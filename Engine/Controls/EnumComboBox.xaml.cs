using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	public partial class EnumComboBox : ComboBox
	{
		public class CheckBoxViewModel : INotifyPropertyChanged
		{
			public string Description { get => _Description; set => SetProperty(ref _Description, value); }
			private string _Description;
			public bool IsChecked { get => _IsChecked; set => SetProperty(ref _IsChecked, value); }
			private bool _IsChecked;


			public AttachmentType Value { get; set; }

			public Visibility LabelVisibility => Value == AttachmentType.None ? Visibility.Visible : Visibility.Collapsed;
			public Visibility CheckVisibility => Value != AttachmentType.None ? Visibility.Visible : Visibility.Collapsed;

			#region ■ INotifyPropertyChanged

			public event PropertyChangedEventHandler PropertyChanged;

			protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
			{
				property = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}

			protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}

			#endregion

		}

		public EnumComboBox()
		{
			InitializeComponent();
			SetItemSource<AttachmentType>();
			Data.ListChanged += List_ListChanged;
		}

		private void List_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.PropertyDescriptor?.Name == nameof(CheckBoxViewModel.IsChecked))
			{
				var items = ItemsSource.Cast<CheckBoxViewModel>();
				var top = items.First(x => x.Value == AttachmentType.None);
				var choice = ItemsSource.Cast<CheckBoxViewModel>().Where(x => x.Value != AttachmentType.None).ToList();
				var count = choice.Count(x => x.IsChecked);
				var names = choice.Where(x => x.IsChecked).Select(x => x.Description).ToList();
				if (count == 0)
					top.Description = "None";
				else if (count == 1)
					top.Description = names.First();
				else
					top.Description = $"{names.First()} + " + (count - 1).ToString();
				var value = Data
					.Where(x => x.IsChecked)
					.Aggregate(default(AttachmentType), (current, item) => current | item.Value);
				if (!Equals(SelectedValue, value))
					SelectedValue = value;
			}
		}

		BindingList<CheckBoxViewModel> Data = new BindingList<CheckBoxViewModel>();

		void SetItemSource<T>()
		{
			var items = Enum.GetValues(typeof(T))
				.Cast<AttachmentType>()
				.Select(e => new CheckBoxViewModel
				{
					Description = GetDescription(e),
					Value = e
				}).ToList();
			Data.Clear();
			items.ForEach(e => Data.Add(e));
			items[0].Description = "None";
			ItemsSource = Data;
		}

		public static string GetDescription(Enum value)
		{
			var field = value.GetType().GetField(value.ToString());
			var attribute = field.GetCustomAttribute<DescriptionAttribute>();
			return attribute?.Description ?? value.ToString();

		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SelectedIndex != 0)
				SelectedIndex = 0;
		}

		#region Binding

		private static readonly new DependencyProperty SelectedValueProperty =
			DependencyProperty.Register("SelectedValue", typeof(AttachmentType), typeof(EnumComboBox),
		new FrameworkPropertyMetadata(default(AttachmentType), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

		public new AttachmentType SelectedValue
		{
			get => (AttachmentType)GetValue(SelectedValueProperty);
			set => SetValue(SelectedValueProperty, value);
		}

		private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var box = (EnumComboBox)d;
			var value = (AttachmentType)box.GetValue(SelectedValueProperty);
			var items = (IEnumerable<CheckBoxViewModel>)box.ItemsSource;
			foreach (var item in items)
			{
				var isChecked = item.Value != AttachmentType.None && value.HasFlag(item.Value);
				if (item.IsChecked != isChecked)
					item.IsChecked = isChecked;
			}
		}

		#endregion
	}
}
