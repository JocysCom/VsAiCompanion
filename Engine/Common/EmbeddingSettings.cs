using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingSettings : NotifyPropertyChanged, IAiServiceModel
	{

		/// <summary>Embedding data source.</summary>
		[DefaultValue("")]
		public string Source
		{
			get => _Source ?? "";
			set => SetProperty(ref _Source, string.IsNullOrWhiteSpace(value) ? "" : value);
		}
		string _Source;

		/// <summary>Embedding data target. Database or folder.</summary>
		[DefaultValue("")]
		public string Target
		{
			get => _Target ?? "";
			set => SetProperty(ref _Target, string.IsNullOrWhiteSpace(value) ? "" : value);
		}
		string _Target;

		#region ■ IAiServiceModel

		[DefaultValue(null)]
		public Guid AiServiceId
		{
			get => _AiServiceId;
			set => SetProperty(ref _AiServiceId, value);
		}
		Guid _AiServiceId;

		[XmlIgnore, JsonIgnore]
		public AiService AiService =>
			Global.AppSettings.AiServices.FirstOrDefault(x => x.Id == AiServiceId);

		[XmlIgnore, JsonIgnore]
		public string AiModel { get => _AiModel; set => SetProperty(ref _AiModel, value); }
		string _AiModel;

		#endregion

	}
}
