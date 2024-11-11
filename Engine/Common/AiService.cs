using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiService : SettingsListFileItem, IHasGuid
	{

		public AiService()
		{
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			_Id = Guid.NewGuid();
		}

		/// <summary>Used by default.</summary>
		[DefaultValue(ApiServiceType.None)]
		public ApiServiceType ServiceType
		{
			get => _ServiceType;
			set
			{
				SetProperty(ref _ServiceType, value);
				Path = JocysCom.ClassLibrary.Runtime.Attributes.GetDescription(value);
				OnPropertyChanged(nameof(Path));
			}
		}
		ApiServiceType _ServiceType;

		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		/// <summary>Used by default.</summary>
		[DefaultValue(false)]
		public bool IsDefault { get => _IsDefault; set => SetProperty(ref _IsDefault, value); }
		bool _IsDefault;

		/// <summary>Stream response data as it becomes available.</summary>
		[DefaultValue(true)]
		public bool ResponseStreaming { get => _ResponseStreaming; set => SetProperty(ref _ResponseStreaming, value); }
		bool _ResponseStreaming;

		/// <summary>Stream response data as it becomes available.</summary>
		[DefaultValue(600)]
		public int ResponseTimeout { get => _ResponseTimeout; set => SetProperty(ref _ResponseTimeout, value); }
		int _ResponseTimeout;

		/// <summary>Configure for Microsoft Azure OpenAI.</summary>
		[DefaultValue(false)]
		public bool IsAzureOpenAI { get => _IsAzureOpenAI; set => SetProperty(ref _IsAzureOpenAI, value); }
		bool _IsAzureOpenAI;

		#region API Keys

		/// <summary>Organization key. Usage from these API requests will count against the specified organization's subscription quota.</summary>
		[XmlIgnore, JsonIgnore]
		public string ApiOrganizationId
		{
			get => AppHelper.UserDecrypt(_ApiOrganizationIdEncrypted);
			set { _ApiOrganizationIdEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiOrganizationId))]
		public string _ApiOrganizationIdEncrypted { get; set; }

		// Vault item

		/// <summary>API Ortanization ID Vault Item ID</summary>
		[DefaultValue(null)]
		public Guid? ApiOrganizationIdVaultItemId { get => _ApiOrganizationIdVaultItemId; set => SetProperty(ref _ApiOrganizationIdVaultItemId, value); }
		Guid? _ApiOrganizationIdVaultItemId;

		public bool ShouldSerializeApiOrganizationIdVaultItemId() => ApiOrganizationIdVaultItemId != null;


		#endregion

		#region ApiAccessKey

		/// <summary>Access Key or Username</summary>
		[XmlIgnore, JsonIgnore]
		public string ApiAccessKey
		{
			get => AppHelper.UserDecrypt(_ApiAccessKeyEncrypted);
			set { _ApiAccessKeyEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiAccessKey))]
		public string _ApiAccessKeyEncrypted { get; set; }

		#endregion

		#region API Secret Key

		/// <summary>Secret Key, API Key or Password.</summary>
		[XmlIgnore, JsonIgnore]
		public string ApiSecretKey
		{
			get => AppHelper.UserDecrypt(_ApiSecretKeyEncrypted);
			set { _ApiSecretKeyEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiSecretKey))]
		public string _ApiSecretKeyEncrypted { get; set; }

		// Vault item

		/// <summary>ApiSecretKey Vault Item ID</summary>
		[DefaultValue(null)]
		public Guid? ApiSecretKeyVaultItemId { get => _ApiSecretKeyVaultItemId; set => SetProperty(ref _ApiSecretKeyVaultItemId, value); }
		Guid? _ApiSecretKeyVaultItemId;

		public bool ShouldSerializeApiSecretKeyVaultItemId() => ApiSecretKeyVaultItemId != null;

		#endregion

		/// <summary>Cache AI Model list.</summary>
		public string DefaultAiModel { get => _DefaultAiModel; set => SetProperty(ref _DefaultAiModel, value); }
		string _DefaultAiModel;

		/// <summary>Cache AI Model list.</summary>
		public string[] AiModels { get => _AiModels; set => SetProperty(ref _AiModels, value); }
		string[] _AiModels;

		/// <summary>Base Url.</summary>
		public string BaseUrl { get => _BaseUrl; set => SetProperty(ref _BaseUrl, value); }
		string _BaseUrl;

		/// <summary>Region used by Microsoft Azure</summary>
		public string Region { get => _Region; set => SetProperty(ref _Region, value); }
		string _Region;

		public string ModelFilter { get => _ModelFilter; set => SetProperty(ref _ModelFilter, value); }
		string _ModelFilter;

	}

}
