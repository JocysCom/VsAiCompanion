using System.Linq;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JocysCom.ClassLibrary.Configuration;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class FineTune : INotifyPropertyChanged, ISettingsItem, IAiServiceModel
	{
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		#region ■ ISettingsItem

		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		bool ISettingsItem.Enabled { get => IsEnabled; set => IsEnabled = value; }

		public bool IsEmpty =>
			string.IsNullOrEmpty(Name);

		#endregion

		#region ■ IAiServiceModel

		public Guid AiServiceId
		{
			get => _AiServiceId;
			set => SetProperty(ref _AiServiceId, value);
		}
		Guid _AiServiceId;

		public AiService AiService =>
			Global.AppSettings.AiServices.FirstOrDefault(x => x.Id == AiServiceId);

		public string AiModel { get => _AiModel; set => SetProperty(ref _AiModel, value); }
		string _AiModel;

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(property, value))
				return;
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

	}
}
