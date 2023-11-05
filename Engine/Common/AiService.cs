using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiService : INotifyPropertyChanged
	{

		public AiService()
		{
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			_Id = Guid.NewGuid();
		}

		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		/// <summary>Name.</summary>
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

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
		[XmlIgnore]
		public string ApiOrganizationId
		{
			get => UserDecrypt(_ApiOrganizationIdEncrypted);
			set { _ApiOrganizationIdEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiOrganizationId))]
		public string _ApiOrganizationIdEncrypted { get; set; }

		/// <summary>Access Key or Username</summary>
		[XmlIgnore]
		public string ApiAccessKey
		{
			get => UserDecrypt(_ApiAccessKeyEncrypted);
			set { _ApiAccessKeyEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiAccessKey))]
		public string _ApiAccessKeyEncrypted { get; set; }


		/// <summary>Secret Key, API Key or Password.</summary>
		[XmlIgnore]
		public string ApiSecretKey
		{
			get => UserDecrypt(_ApiSecretKeyEncrypted);
			set { _ApiSecretKeyEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiSecretKey))]
		public string _ApiSecretKeyEncrypted { get; set; }

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

		public string ModelFilter { get => _ModelFilter; set => SetProperty(ref _ModelFilter, value); }
		string _ModelFilter;

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

		#region INotifyPropertyChanged

		// CWE-502: Deserialization of Untrusted Data
		// Fix: Apply [field: NonSerialized] attribute to an event inside class with [Serialized] attribute.
		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if (handler != null)
			{
				if (ControlsHelper.MainTaskScheduler == null)
					handler(this, new PropertyChangedEventArgs(propertyName));
				else
					ControlsHelper.Invoke(handler, this, new PropertyChangedEventArgs(propertyName));
			}
		}

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion


	}

}
