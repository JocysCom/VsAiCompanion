using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public class VaultItem : SettingsListFileItem
	{
		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		/// <summary>Used by default.</summary>
		[DefaultValue(ApiServiceType.None)]
		public ApiServiceType ServiceType { get => _ServiceType; set => SetProperty(ref _ServiceType, value); }
		ApiServiceType _ServiceType;

		/// <summary>Vault Name.</summary>
		[DefaultValue(null)]
		public string VaultName { get => _VaultName; set => SetProperty(ref _VaultName, value); }
		string _VaultName;

		/// <summary>Vault Item/Secret/Key/Certificate Name.</summary>
		[DefaultValue(null)]
		public string VaultItemName { get => _VaultItemName; set => SetProperty(ref _VaultItemName, value); }
		string _VaultItemName;

		/// <summary>Activation Date.</summary>
		[DefaultValue(null)]
		public DateTimeOffset? ActivationDate { get => _ActivationDate; set => SetProperty(ref _ActivationDate, value); }
		DateTimeOffset? _ActivationDate;

		/// <summary>Expiration Date.</summary>
		[DefaultValue(null)]
		public DateTimeOffset? ExpirationDate { get => _ExpirationDate; set => SetProperty(ref _ExpirationDate, value); }
		DateTimeOffset? _ExpirationDate;

		/// <summary>Vault secret value.</summary>
		[XmlIgnore, JsonIgnore]
		[DefaultValue(null)]
		public string Value
		{
			get => AppHelper.UserDecrypt(_ValueEncrypted);
			set { _ValueEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(Value))]
		public string _ValueEncrypted { get; set; }

	}
}
