using JocysCom.ClassLibrary.Configuration;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
		public string VaultName { get => _VaultName; set => SetProperty(ref _VaultName, value); }
		string _VaultName;

		/// <summary>Vault Item/Secret/Key/Certificate Name.</summary>
		public string VaultItemName { get => _VaultItemName; set => SetProperty(ref _VaultItemName, value); }
		string _VaultItemName;
	}
}
