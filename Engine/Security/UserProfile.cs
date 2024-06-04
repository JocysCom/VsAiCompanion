using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class UserProfile : NotifyPropertyChanged
	{
		public UserProfile()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		[DefaultValue(null)]
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		private string _Name;

		[DefaultValue(null)]
		public string Username { get => _Username; set => SetProperty(ref _Username, value); }
		private string _Username;

		[DefaultValue(null)]
		/// <summary>Unique identifier for the account</summary>
		public string AccountId { get => _AccountId; set => SetProperty(ref _AccountId, value); }
		private string _AccountId;

		[DefaultValue(null)]
		public string Email { get => _Email; set => SetProperty(ref _Email, value); }
		private string _Email;

		[DefaultValue(ApiServiceType.Azure)]
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


		/// <summary>ID token.</summary>
		[XmlIgnore, JsonIgnore]
		public string IdToken
		{
			get => AppHelper.UserDecrypt(_IdTokenEncrypted);
			set { _IdTokenEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(IdToken))]
		public string _IdTokenEncrypted { get; set; }


		[DefaultValue(null), XmlIgnore, JsonIgnore]
		public ImageSource Image { get => _Image; set => SetProperty(ref _Image, value); }
		private ImageSource _Image;

		public void Clear()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Use windows user if empty.
		/// </summary>
		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(Username);
		}

	}

}
