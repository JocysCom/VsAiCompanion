using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.ClassLibrary.ComponentModel
{
	public class NotifyPropertyChanged : INotifyPropertyChanged
	{

		#region ■ INotifyPropertyChanged

		/// <summary>
		/// Notifies clients that a property value has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(property, value))
				return;
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Rase event that notifies clients that a property value has changed.
		/// </summary>
		/// <param name="propertyName">Name of the property.</param>
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}
