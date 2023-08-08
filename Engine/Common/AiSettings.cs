using JocysCom.ClassLibrary.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// AI Name/URL key pair.
	/// </summary>
	public class AiSettings : IKeyValue<string, string>
	{

		/// <summary>AI Name.</summary>
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		/// <summary>API URL.</summary>
		public string URL { get => _URL; set => SetProperty(ref _URL, value); }
		string _URL;

		/// <summary>Cache AI Model list.</summary>
		public string[] Models { get => _Models; set => SetProperty(ref _Models, value); }
		string[] _Models;

		#region ■ IKeyValue<string, string>

		string IKeyValue<string, string>.Key { get => Name; set => Name = value; }
		string IKeyValue<string, string>.Value { get => URL; set => URL = value; }

		#endregion

		#region ■ INotifyPropertyChanged

		// CWE-502: Deserialization of Untrusted Data
		// Fix: Apply [field: NonSerialized] attribute to an event inside class with [Serialized] attribute.
		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		internal void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
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
