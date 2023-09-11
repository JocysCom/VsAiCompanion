using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PromptItem : INotifyPropertyChanged
	{
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;
		public string Pattern { get => _Pattern; set => SetProperty(ref _Pattern, value); }
		string _Pattern;

		[System.Xml.Serialization.XmlArrayItem("Option", IsNullable = false)]
		public BindingList<string> Options {
			get => _Options = _Options ?? new BindingList<string>();
			set => SetProperty(ref _Options, value); }
		BindingList<string> _Options;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion


	}
}
