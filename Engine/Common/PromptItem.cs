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

		[System.Xml.Serialization.XmlArrayItem("Option", IsNullable = false)]
		public BindingList<string> Options
		{
			get => _Options = _Options ?? new BindingList<string>();
			set => SetProperty(ref _Options, value);
		}
		BindingList<string> _Options;

	}
}
