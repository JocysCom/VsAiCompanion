using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PromptItem : NotifyPropertyChanged
	{
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		public string Pattern { get => _Pattern; set => SetProperty(ref _Pattern, value); }
		string _Pattern;

		[DefaultValue(RisenType.None)]
		public RisenType RisenType { get => _RisenType; set => SetProperty(ref _RisenType, value); }
		RisenType _RisenType;

		// Put the property with the largest content at the bottom so that it will be serialized at the end of the XML/JSON.

		/// <summary>Prompt options.</summary>
		[System.Xml.Serialization.XmlArrayItem("Option", IsNullable = false)]
		public BindingList<string> Options
		{
			get => _Options = _Options ?? new BindingList<string>();
			set => SetProperty(ref _Options, value);
		}
		BindingList<string> _Options;

	}
}
