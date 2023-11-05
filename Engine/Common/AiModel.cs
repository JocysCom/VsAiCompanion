using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiModel : INotifyPropertyChanged
	{
		public AiModel() { }
		public AiModel(string name, Guid aiServiceId)
		{
			Id = AppHelper.GetGuid(GetType().Name, name);
			Name = name;
			AiServiceId = aiServiceId;
		}

		/// <summary>Unique Id.</summary>
		[Key]
		public Guid Id { get => _Id; set => SetProperty(ref _Id, value); }
		Guid _Id;

		/// <summary>Name.</summary>
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		public bool AllowFineTuning { get => _AllowFineTuning; set => SetProperty(ref _AllowFineTuning, value); }
		bool _AllowFineTuning;

		public Guid AiServiceId { get => _AiServiceId; set => SetProperty(ref _AiServiceId, value); }
		Guid _AiServiceId;

		#region ■ INotifyPropertyChanged

		// CWE-502: Deserialization of Untrusted Data
		// Fix: Apply [field: NonSerialized] attribute to an event inside class with [Serialized] attribute.
		[field: NonSerialized]
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
