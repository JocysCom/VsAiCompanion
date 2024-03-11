using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiFileListItem : SettingsListFileItem, IAiServiceModel, ICancellationTokens
	{
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

	}
}
