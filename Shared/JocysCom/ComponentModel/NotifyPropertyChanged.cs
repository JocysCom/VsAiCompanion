using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace JocysCom.ClassLibrary.ComponentModel
{
	public class NotifyPropertyChanged : INotifyPropertyChanged
	{

		#region ■ INotifyPropertyChanged

		/// <summary>
		/// Notifies clients that a property value has changed.
		/// </summary>
		// CWE-502: Deserialization of Untrusted Data
		// Fix: Apply [field: NonSerialized] attribute to an event inside class with [Serialized] attribute.
		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Rase event that notifies clients that a property value has changed.
		/// </summary>
		/// <param name="propertyName">Name of the property.</param>
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			if (UseApplicationDispatcher)
			{
				Application.Current.Dispatcher.Invoke(() =>
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
				return;
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		[field: NonSerialized]
		public bool UseApplicationDispatcher = false;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(property, value))
				return;
			property = value;
			// Invoke overridden OnPropertyChanged method in the most derived class of the object.
			OnPropertyChanged(propertyName);
		}


		#endregion
	}
}
