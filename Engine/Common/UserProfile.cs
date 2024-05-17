using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class UserProfile : NotifyPropertyChanged
	{
		public string Username { get => _Username; set => SetProperty(ref _Username, value); }
		private string _Username;

		/// <summary>Unique identifier for the account</summary>
		public string AccountId { get => _AccountId; set => SetProperty(ref _AccountId, value); }
		private string _AccountId;

		public string Email { get => _Email; set => SetProperty(ref _Email, value); }
		private string _Email;

		public ApiServiceType ServiceType { get => _ServiceType; set => SetProperty(ref _ServiceType, value); }
		private ApiServiceType _ServiceType;

		/// <summary>Access token.</summary>
		[XmlIgnore, JsonIgnore]
		public string AccessToken
		{
			get => AppHelper.UserDecrypt(_AccessTokenEncrypted);
			set { _AccessTokenEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(AccessToken))]
		public string _AccessTokenEncrypted { get; set; }

	}

}
