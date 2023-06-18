using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public class SettingsBase: ISettings, INotifyPropertyChanged
	{

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

		#region Encrypt Settings 

		internal static string UserEncrypt(string text)
		{
			try
			{
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
