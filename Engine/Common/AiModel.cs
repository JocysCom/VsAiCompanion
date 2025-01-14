using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiModel : SettingsListFileItem
	{
		public AiModel()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		public AiModel(string name, Guid aiServiceId)
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			Id = AppHelper.GetGuid(GetType().Name, aiServiceId, name);
			Name = name;
			AiServiceId = aiServiceId;
		}

		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		public bool AllowFineTuning { get => _AllowFineTuning; set => SetProperty(ref _AllowFineTuning, value); }
		bool _AllowFineTuning;

		public Guid AiServiceId { get => _AiServiceId; set => SetProperty(ref _AiServiceId, value); }
		Guid _AiServiceId;

		[XmlIgnore, JsonIgnore]
		public string AiServiceName { get => Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == AiServiceId)?.Name; }

		[DefaultValue(0)]
		public int MaxInputTokens { get => _MaxInputTokens; set => SetProperty(ref _MaxInputTokens, value); }
		int _MaxInputTokens;

		[DefaultValue(0)]
		public int MaxOutputTokens { get => _MaxOutputTokens; set => SetProperty(ref _MaxOutputTokens, value); }
		int _MaxOutputTokens;

		[DefaultValue(AiModelFeatures.None)]
		public AiModelFeatures Features { get => _Features; set => SetProperty(ref _Features, value); }
		AiModelFeatures _Features;

		[DefaultValue(false)]
		public bool IsFeaturesKnown { get => _IsFeaturesKnown; set => SetProperty(ref _IsFeaturesKnown, value); }
		bool _IsFeaturesKnown;
		public bool HasFeature(AiModelFeatures feature)
			// Assume that feature is supported if it is not known.
			// User option will be used.
			=> !IsFeaturesKnown || Features.HasFlag(feature);

		[DefaultValue(typeof(decimal), "0")]
		public decimal InputCostPerMillionTokens
		{
			get => _InputCostPerMillionTokens;
			set => SetProperty(ref _InputCostPerMillionTokens, value);
		}
		private decimal _InputCostPerMillionTokens;

		[DefaultValue(typeof(decimal), "0")]
		public decimal OutputCostPerMillionTokens
		{
			get => _OutputCostPerMillionTokens;
			set => SetProperty(ref _OutputCostPerMillionTokens, value);
		}
		private decimal _OutputCostPerMillionTokens;

		/// <summary>Model Instructions that will be included at the start of every message.</summary>
		[DefaultValue("")]
		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, string.IsNullOrWhiteSpace(value) ? "" : value); }
		string _Instructions;

		/// <summary>Enable model instructions.</summary>
		[DefaultValue(false)]
		public bool InstructionsEnabled { get => _InstructionsEnabled; set => SetProperty(ref _InstructionsEnabled, value); }
		bool _InstructionsEnabled;


	}
}
