using System.ComponentModel;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class Settings : SettingsBase
	{
		/// <summary>Organization key. Usage from these API requests will count against the specified organization's subscription quota.</summary>
		[XmlIgnore]
		public string ApiOrganizationId
		{
			get => UserDecrypt(_ApiOrganizationIdEncrypted);
			set { _ApiOrganizationIdEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(ApiOrganizationId))]
		public string _ApiOrganizationIdEncrypted { get; set; }


		public const string AiModelDefault = "gpt-3.5-turbo-16k-0613";

		/// <summary>Cache AI Model list.</summary>
		public string[] AiModels { get => _AiModels; set => SetProperty(ref _AiModels, value); }
		string[] _AiModels = new string[] {
			"text-davinci-003",
			"text-davinci-002",
			"text-davinci-001",
			"gpt-3.5-turbo-16k-0613",
			"gpt-3.5-turbo-16k",
			"gpt-3.5-turbo-0613",
			"gpt-3.5-turbo-0301",
			"gpt-3.5-turbo"
		};

		/// <summary>Cache AI Model list.</summary>
		public string ModelFilter { get => _ModelFilter; set => SetProperty(ref _ModelFilter, value); }
		string _ModelFilter = "gpt|text-davinci-[0-9+]";

	}

}
