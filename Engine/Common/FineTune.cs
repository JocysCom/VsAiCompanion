using JocysCom.ClassLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class FineTune : INotifyPropertyChanged, ISettingsItem, IAiServiceModel, ICancellationTokens
	{
		public FineTune()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		public static string TuningData = "TuningData";
		public static string SourceData = "SourceData";

		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[DefaultValue("data.json")]
		public string JsonListFile { get => _JsonListFile; set => SetProperty(ref _JsonListFile, value); }
		string _JsonListFile;

		[DefaultValue("data.jsonl")]
		public string JsonLinesFile { get => _JsonLinesFile; set => SetProperty(ref _JsonLinesFile, value); }
		string _JsonLinesFile;

		#region App Settings Selections

		public List<string> FineTuningSourceDataSelection { get => _FineTuningSourceDataSelection; set => SetProperty(ref _FineTuningSourceDataSelection, value); }
		List<string> _FineTuningSourceDataSelection;

		public List<string> FineTuningTuningDataSelection { get => _FineTuningTuningDataSelection; set => SetProperty(ref _FineTuningTuningDataSelection, value); }
		List<string> _FineTuningTuningDataSelection;

		public List<string> FineTuningRemoteDataSelection { get => _FineTuningRemoteDataSelection; set => SetProperty(ref _FineTuningRemoteDataSelection, value); }
		List<string> _FineTuningRemoteDataSelection;

		public List<string> FineTuningJobListSelection { get => _FineTuningJobListSelection; set => SetProperty(ref _FineTuningJobListSelection, value); }
		List<string> _FineTuningJobListSelection;

		public List<string> FineTuningModelListSelection { get => _FineTuningModelListSelection; set => SetProperty(ref _FineTuningModelListSelection, value); }
		List<string> _FineTuningModelListSelection;
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
