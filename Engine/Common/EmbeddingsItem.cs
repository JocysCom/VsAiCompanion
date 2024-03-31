using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.DataClient;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingsItem : SettingsListFileItem, IAiServiceModel
	{

		public EmbeddingsItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

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

		/// <summary>
		/// Monitor source and auto-update target.
		/// </summary>
		[DefaultValue(false)]
		public bool AutoUpdate { get => _AutoUpdate; set => SetProperty(ref _AutoUpdate, value); }
		bool _AutoUpdate;

		/// <summary>
		/// Test message
		/// </summary>
		[DefaultValue("")]
		public string Message { get => _Message; set => SetProperty(ref _Message, string.IsNullOrWhiteSpace(value) ? "" : value); }
		string _Message;

		/// <summary>
		/// System instructions
		/// </summary>
		[DefaultValue("")]
		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, string.IsNullOrWhiteSpace(value) ? "" : value); }
		string _Instructions;

		[DefaultValue("")]
		public string EmbeddingGroupName { get => _EmbeddingGroupName; set => SetProperty(ref _EmbeddingGroupName, value); }
		string _EmbeddingGroupName;

		[DefaultValue(EmbeddingGroup.None)]
		public EmbeddingGroup EmbeddingGroupFlag { get => _EmbeddingGroupFlag; set => SetProperty(ref _EmbeddingGroupFlag, value); }
		EmbeddingGroup _EmbeddingGroupFlag;

		#region ■ IAiServiceModel

		[DefaultValue(null)]
		public Guid AiServiceId
		{
			get => _AiServiceId;
			set => SetProperty(ref _AiServiceId, value);
		}
		Guid _AiServiceId;


		[DefaultValue(0)]
		public int Skip { get => _Skip; set => SetProperty(ref _Skip, value); }
		int _Skip;

		[DefaultValue(4)]
		public int Take { get => _Take; set => SetProperty(ref _Take, value); }
		int _Take;

		[DefaultValue(32768)]
		public int MaxTokens { get => _MaxTokens; set => SetProperty(ref _MaxTokens, value); }
		int _MaxTokens;

		[XmlIgnore, JsonIgnore]
		public AiService AiService =>
			Global.AppSettings.AiServices.FirstOrDefault(x => x.Id == AiServiceId);

		public string AiModel { get => _AiModel; set => SetProperty(ref _AiModel, value); }
		string _AiModel;

		#endregion

	}
}
