using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;

namespace JocysCom.ClassLibrary.Configuration
{
	/// <inheritdoc />
	public class SettingsItem : NotifyPropertyChanged, ISettingsItem
	{
		/// <inheritdoc />
		[DefaultValue(true)]
		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled = true;

		/// <inheritdoc />
		public virtual bool IsEmpty => true;

	}
}
