using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Controls
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
}
