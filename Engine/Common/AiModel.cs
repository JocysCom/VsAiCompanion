using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiModel : NotifyPropertyChanged
	{
		public AiModel() { }
		public AiModel(string name, Guid aiServiceId)
		{
			Id = AppHelper.GetGuid(GetType().Name, name);
			Name = name;
			AiServiceId = aiServiceId;
		}

		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		/// <summary>Name.</summary>
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		public bool AllowFineTuning { get => _AllowFineTuning; set => SetProperty(ref _AllowFineTuning, value); }
		bool _AllowFineTuning;

		public Guid AiServiceId { get => _AiServiceId; set => SetProperty(ref _AiServiceId, value); }
		Guid _AiServiceId;

		[XmlIgnore, JsonIgnore]
		public string AiServiceName { get => Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == AiServiceId)?.Name; }


		[DefaultValue(0)]
		public int MaxInputTokens { get => _MaxInputTokens; set => SetProperty(ref _MaxInputTokens, value); }
		int _MaxInputTokens;

	}
}
