using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class MailAccount : SettingsListFileItem
	{
		public MailAccount()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>Email Name</summary>
		[DefaultValue("")]
		public string EmailName { get => _EmailName; set => SetProperty(ref _EmailName, value); }
		string _EmailName;

		/// <summary>Email Address</summary>
		[DefaultValue("")]
		public string EmailAddress { get => _EmailAddress; set => SetProperty(ref _EmailAddress, value); }
		string _EmailAddress;

		/// <summary>IMAP server host</summary>
		[DefaultValue("")]
		public string ImapHost { get => _ImapHost; set => SetProperty(ref _ImapHost, value); }
		string _ImapHost;

		/// <summary>SMTP server host</summary>
		[DefaultValue("")]
		public string SmtpHost { get => _SmtpHost; set => SetProperty(ref _SmtpHost, value); }
		string _SmtpHost;

		/// <summary>Server Port</summary>
		[DefaultValue(993)]
		public int ImapPort { get => _ImapPort; set => SetProperty(ref _ImapPort, value); }
		int _ImapPort;

		/// <summary>IMAP Connection Security</summary>
		[DefaultValue(MailConnectionSecurity.SslOnConnect)]
		public MailConnectionSecurity ImapSecurity { get => _ImapSecurity; set => SetProperty(ref _ImapSecurity, value); }
		MailConnectionSecurity _ImapSecurity;

		/// <summary>Server Port</summary>
		[DefaultValue(465)]
		public int SmtpPort { get => _SmtpPort; set => SetProperty(ref _SmtpPort, value); }
		int _SmtpPort;

		/// <summary>SMTP Connection Security</summary>
		[DefaultValue(MailConnectionSecurity.SslOnConnect)]
		public MailConnectionSecurity SmtpSecurity { get => _SmtpSecurity; set => SetProperty(ref _SmtpSecurity, value); }
		MailConnectionSecurity _SmtpSecurity;


		/// <summary>Username</summary>
		[DefaultValue("")]
		public string Username { get => _Username; set => SetProperty(ref _Username, value); }
		string _Username;

		/// <summary>Organization key. Usage from these API requests will count against the specified organization's subscription quota.</summary>
		[XmlIgnore, JsonIgnore]
		public string Password
		{
			get => UserDecrypt(_PasswordEncrypted);
			set { _PasswordEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(Password))]
		public string _PasswordEncrypted { get; set; }

		#region Communication Security

		/// <summary>
		/// Emails that are allowed to communicate with the AI on this account.
		/// </summary>
		[DefaultValue("")]
		public string AllowedSenders { get => _AllowedSenders; set => SetProperty(ref _AllowedSenders, value); }
		string _AllowedSenders;

		/// <summary>
		/// Emails that are allowed to receive replies from this account.
		/// </summary>
		[DefaultValue("")]
		public string AllowedRecipients { get => _AllowedRecipients; set => SetProperty(ref _AllowedRecipients, value); }
		string _AllowedRecipients;

		/// <summary>
		/// Enable Allowed Senders filter.
		/// </summary>
		[DefaultValue(true)]
		public bool ValidateSenders { get => _ValidateSenders; set => SetProperty(ref _ValidateSenders, value); }
		bool _ValidateSenders;

		/// <summary>
		/// Enable Allowed Recipients filter.
		/// </summary>
		[DefaultValue(true)]
		public bool ValidateRecipients { get => _ValidateRecipients; set => SetProperty(ref _ValidateRecipients, value); }
		bool _ValidateRecipients;

		/// <summary>
		/// Require valid digital signature.
		/// </summary>
		[DefaultValue(true)]
		public bool ValidateDigitalSignature { get => _ValidateDigitalSignature; set => SetProperty(ref _ValidateDigitalSignature, value); }
		bool _ValidateDigitalSignature;

		/// <summary>
		/// Require to pass DKIM validation.
		/// </summary>
		[DefaultValue(true)]
		public bool ValidateDkim { get => _ValidateDkim; set => SetProperty(ref _ValidateDkim, value); }
		bool _ValidateDkim;


		/// <summary>
		/// Trust server certificate.
		/// </summary>
		[DefaultValue(false)]
		public bool TrustServerCertificate { get => _TrustServerCertificate; set => SetProperty(ref _TrustServerCertificate, value); }
		bool _TrustServerCertificate;

		#endregion

		#region Encrypt Settings 

		internal static string UserEncrypt(string text)
		{
			try
			{
				if (string.IsNullOrEmpty(text))
					return null;
				//var user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
				var user = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Encrypt(text, user);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		internal static string UserDecrypt(string base64)
		{
			try
			{
				if (string.IsNullOrEmpty(base64))
					return null;
				//var user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
				var user = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Decrypt(base64, user);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		#endregion

	}
}
