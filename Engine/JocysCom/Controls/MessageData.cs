using JocysCom.VS.AiCompanion.Engine;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.ClassLibrary.Controls.Chat
{
	public class MessageAttachments : INotifyPropertyChanged
	{

		public AttachmentType Type { get => _Type; set => SetProperty(ref _Type, value); }
		AttachmentType _Type;

		public string Title { get => _Title; set => SetProperty(ref _Title, value); }
		string _Title;

		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, value); }
		string _Instructions;

		/// <summary>Optional context data: cliboard, selection or Files.</summary>
		public string Data { get => _Data; set => SetProperty(ref _Data, value); }
		string _Data;

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
