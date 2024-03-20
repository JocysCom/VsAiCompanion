using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{

	/// <summary>TTS Message</summary>
	[DataContract]
	public class @message : NotifyPropertyChanged
	{

		/// <summary>Character name.</summary>
		[DataMember, XmlAttribute]
		public string name { get { return _name; } set { _name = value; OnPropertyChanged(); } }
		string _name;

		/// <summary>
		/// Culture. Can be sent in 2 formats:
		///		LCID HEX value: '419' = 0x0419 = ru-RU = Russian - Russia // Regex: ^[0-9a-fA-F]{1,4}$
		///		Language[-Location] value: 'en-GB' = English - Great Britain
		/// var ci = new System.Globalization.CultureInfo("en-GB", false);
		/// var ci = new System.Globalization.CultureInfo(0x040A, false);
		/// </summary>
		[DataMember, XmlAttribute, DefaultValue("")]
		public string language { get { return _language; } set { _language = value; OnPropertyChanged(); } }
		string _language;

		/// <summary></summary>
		[DataMember, XmlAttribute]
		public string command { get { return _command; } set { _command = value; OnPropertyChanged(); } }
		string _command;

		/// <summary></summary>
		[DataMember, XmlAttribute]
		public string pitch { get { return _pitch; } set { _pitch = value; OnPropertyChanged(); } }
		string _pitch;

		/// <summary></summary>
		[DataMember, XmlAttribute]
		public string rate { get { return _rate; } set { _rate = value; OnPropertyChanged(); } }
		string _rate;

		/// <summary></summary>
		[DataMember, XmlAttribute]
		public string gender { get { return _gender; } set { _gender = value; OnPropertyChanged(); } }
		string _gender;

		/// <summary></summary>
		[DataMember, XmlAttribute]
		public string effect { get { return _effect; } set { _effect = value; OnPropertyChanged(); } }
		string _effect;

		/// <summary></summary>
		[DataMember(EmitDefaultValue = false), DefaultValue(null), XmlAttribute]
		public string group { get { return _group; } set { _group = value; OnPropertyChanged(); } }
		string _group;

		/// <summary>Voice volume. Range from 0 to 100. Default: 100.</summary>
		[DataMember(EmitDefaultValue = false), DefaultValue(null), XmlAttribute]
		public string volume { get { return _volume; } set { _volume = value; OnPropertyChanged(); } }
		string _volume;


		/// <summary>Text to play.</summary>
		[DataMember(EmitDefaultValue = false), DefaultValue(null), XmlElement]
		public string part { get { return _part; } set { _part = value; OnPropertyChanged(); } }
		string _part;


	}

}

