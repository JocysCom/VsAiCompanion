using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Media;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class FileListItem : IFileListItem, INotifyPropertyChanged, IAiServiceModel, ICancellationTokens, ISettingsItem
	{
		#region ■ IFileListItem

		public bool IsChecked { get => _IsChecked; set => SetProperty(ref _IsChecked, value); }
		bool _IsChecked;

		public string StatusText { get => _StatusText; set => SetProperty(ref _StatusText, value); }
		string _StatusText;
		public System.Windows.MessageBoxImage StatusCode { get => _StatusCode; set => SetProperty(ref _StatusCode, value); }
		System.Windows.MessageBoxImage _StatusCode;

		[XmlIgnore]
		public DrawingImage Icon { get => _Icon; }
		DrawingImage _Icon;

		public string IconType { get => _IconType; set => SetProperty(ref _IconType, value); }
		string _IconType;

		public string IconData { get => _IconData; set => SetProperty(ref _IconData, value); }
		string _IconData;

		public void SetIcon(string contents, string type = ".svg")
		{
			var base64 = Converters.SvgHelper.GetBase64(contents);
			IconType = type;
			IconData = base64;
		}

		#endregion

		#region ■ ISettingsItemFile
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[XmlIgnore]
		string ISettingsItemFile.BaseName { get => Name; set => Name = value; }

		[XmlIgnore]
		DateTime ISettingsItemFile.WriteTime { get; set; }

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(property, value))
				return;
			if (propertyName == nameof(IconData))
			{
				var svgContent = Converters.SvgHelper.GetContent((string)(object)value);
				_Icon = Converters.SvgHelper.LoadSvgFromString(svgContent);
				OnPropertyChanged(nameof(Icon));
			}
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			((ISettingsItemFile)this).WriteTime = DateTime.Now;
		}

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

		#region ■ ICancellationTokens

		[XmlIgnore, DefaultValue(false)]
		public bool IsBusy { get => _IsBusy; set => SetProperty(ref _IsBusy, value); }
		bool _IsBusy;

		[XmlIgnore, DefaultValue(null)]
		public BindingList<CancellationTokenSource> CancellationTokenSources { get => _CancellationTokenSources; set => SetProperty(ref _CancellationTokenSources, value); }
		BindingList<CancellationTokenSource> _CancellationTokenSources;

		public void StopClients()
		{
			var clients = CancellationTokenSources.ToArray();
			try
			{
				foreach (var client in clients)
					client.Cancel();
			}
			catch { }
		}

		#endregion

		#region ■ ISettingsItem

		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		bool ISettingsItem.Enabled { get => IsEnabled; set => IsEnabled = value; }

		public bool IsEmpty =>
			string.IsNullOrEmpty(Name);

		#endregion

	}
}
