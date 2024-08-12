using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.DataClient.Common;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingsItem : SettingsListFileItem, IAiServiceModel
	{

		public EmbeddingsItem() : base()
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

		/// <summary>
		/// Specify group name manually and override.
		/// </summary>
		[DefaultValue(false)]
		public bool OverrideGroupName { get => _OverrideGroupName; set => SetProperty(ref _OverrideGroupName, value); }
		bool _OverrideGroupName;

		/// <summary>
		/// Specify group flag manually and override.
		/// </summary>
		[DefaultValue(false)]
		public bool OverrideGroupFlag { get => _OverrideGroupFlag; set => SetProperty(ref _OverrideGroupFlag, value); }
		bool _OverrideGroupFlag;

		[DefaultValue("")]
		public string EmbeddingGroupName { get => _EmbeddingGroupName; set => SetProperty(ref _EmbeddingGroupName, value); }
		string _EmbeddingGroupName;

		[DefaultValue(EmbeddingGroupFlag.None)]
		public EmbeddingGroupFlag EmbeddingGroupFlag { get => _EmbeddingGroupFlag; set => SetProperty(ref _EmbeddingGroupFlag, value); }
		EmbeddingGroupFlag _EmbeddingGroupFlag;

		[DefaultValue("")]
		public string EmbeddingGroupFlagName { get => _EmbeddingGroupFlagName; set => SetProperty(ref _EmbeddingGroupFlagName, value); }
		string _EmbeddingGroupFlagName;

		/// <summary>
		/// Use .gitignore to filter files.
		/// </summary>
		[DefaultValue(true)]
		public bool UseGitIgnore { get => _UseGitIgnore; set => SetProperty(ref _UseGitIgnore, value); }
		bool _UseGitIgnore;

		[DefaultValue("*.*")]
		public string SourcePattern { get => _SourcePattern; set => SetProperty(ref _SourcePattern, string.IsNullOrWhiteSpace(value) ? "*.*" : value); }
		string _SourcePattern;

		[DefaultValue("")]
		public string IncludePatterns { get => _IncludePatterns; set => SetProperty(ref _IncludePatterns, string.IsNullOrWhiteSpace(value) ? "" : value); }
		string _IncludePatterns;

		[DefaultValue("")]
		public string ExcludePatterns { get => _ExcludePatterns; set => SetProperty(ref _ExcludePatterns, string.IsNullOrWhiteSpace(value) ? "" : value); }
		string _ExcludePatterns;

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

		[DefaultValue(16)]
		public int Take { get => _Take; set => SetProperty(ref _Take, value); }
		int _Take;

		/// <summary>
		/// TODO: some min size or ~25% of the maximum input tokens.
		/// </summary>
		[DefaultValue(32768)]
		public int MaxTokens { get => _MaxTokens; set => SetProperty(ref _MaxTokens, value); }
		int _MaxTokens;

		[XmlIgnore, JsonIgnore]
		public AiService AiService
			=> Global.AppSettings.AiServices.FirstOrDefault(x => x.Id == AiServiceId);

		public string AiModel { get => _AiModel; set => SetProperty(ref _AiModel, value); }
		string _AiModel;

		#endregion

	}
}
