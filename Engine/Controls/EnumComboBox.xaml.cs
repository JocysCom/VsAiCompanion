using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	public partial class EnumComboBox : ComboBox, INotifyPropertyChanged
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
			list.ListChanged += List_ListChanged;
		}

		private void List_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.PropertyDescriptor?.Name == nameof(CheckBoxViewModel.IsChecked))
			{
				var count = ItemsSource.Cast<CheckBoxViewModel>().Count(x => x.IsChecked);
				list[0].Description = $"Attachments ({count})";
			}

		}

		BindingList<CheckBoxViewModel> list = new BindingList<CheckBoxViewModel>();

		public void SetItemSource<T>()
		{
			var items = Enum.GetValues(typeof(T))
				.Cast<AttachmentType>()
				.Select(e => new CheckBoxViewModel
				{
					Description = GetDescription(e),
					Value = e
				}).ToList();
			list.Clear();
			items.ForEach(e => list.Add(e));
			list[0].Description = $"Attachments";
			ItemsSource = items;
		}

		public static string GetDescription(Enum value)
		{
			var field = value.GetType().GetField(value.ToString());
			var attribute = field.GetCustomAttribute<DescriptionAttribute>();
			return attribute?.Description ?? value.ToString();

		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SelectedIndex = 0;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
