using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
		public string AccountId
		{
			get => _AccountId;
			set { SetProperty(ref _AccountId, value); OnPropertyChanged(nameof(IsSignedIn)); }
		}
		private string _AccountId;

		/// <summary>
		/// Property of AzureAD/EntraID user.
		/// </summary>
		[DefaultValue(null)]
		public string TenantId { get => _TenantId; set => SetProperty(ref _TenantId, value); }
		private string _TenantId;

		[DefaultValue(null)]
		public string Email { get => _Email; set => SetProperty(ref _Email, value); }
		private string _Email;

		[DefaultValue(ApiServiceType.Azure)]
		public ApiServiceType ServiceType { get => _ServiceType; set => SetProperty(ref _ServiceType, value); }
		private ApiServiceType _ServiceType;

		/// <summary>Access tokens.</summary>
		public List<KeyValue> Tokens
		{
			get => _Tokens = _Tokens ?? new List<KeyValue>();
			set => SetProperty(ref _Tokens, value);
		}
		List<KeyValue> _Tokens;

		/// <summary>ID token.</summary>
		[XmlIgnore, JsonIgnore]
		public string IdToken
		{
			get => AppHelper.UserDecrypt(_IdTokenEncrypted);
			set { _IdTokenEncrypted = AppHelper.UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(IdToken))]
		public string _IdTokenEncrypted { get; set; }

		#region Token Methods

		/// <summary>
		/// Get a decrypted token from the stored tokens list by specifying the scope.
		/// </summary>
		public string GetToken(params string[] scopes)
		{
			var key = string.Join(" ", scopes);
			var token = Tokens.FirstOrDefault(x => x.Key == key);
			var decryptedValue = AppHelper.UserDecrypt(token?.Value);
			return decryptedValue;
		}

		/// <summary>
		/// Save a encrypted token to the stored tokens list by the scope.
		/// </summary>
		public string SetToken(string value, params string[] scopes)
		{
			var key = string.Join(" ", scopes);
			var token = Tokens.FirstOrDefault(x => x.Key == key);
			if (token == null)
			{
				token = new KeyValue { Key = key };
				Tokens.Add(token);
			}
			var encryptedValue = AppHelper.UserEncrypt(value);
			token.Value = encryptedValue;
			return value;
		}

		#endregion

		#region Non-Serialized Propertied

		[XmlIgnore, JsonIgnore]
		public bool IsSignedIn => !string.IsNullOrEmpty(AccountId);


		[DefaultValue(null), XmlIgnore, JsonIgnore]
		public ImageSource Image { get => _Image; set => SetProperty(ref _Image, value); }
		private ImageSource _Image;

		[DefaultValue(null), XmlIgnore, JsonIgnore]
		public List<string> UserGroups { get => _UserGroups; set => SetProperty(ref _UserGroups, value); }
		private List<string> _UserGroups;

		#endregion

		#region Methods

		public void Clear()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			Tokens.Clear();
		}

		/// <summary>
		/// Use windows user if empty.
		/// </summary>
		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(Username);
		}

		#endregion

	}

}
