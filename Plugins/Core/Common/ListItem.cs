using JocysCom.ClassLibrary.ComponentModel;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Represents a versatile list item used in various contexts such as TODO lists, environment variable management, and diverse list compilations.
	/// Structured as a key-value pair with an optional comment, facilitating a broad spectrum of applications from settings configuration to task management.
	/// Aides in organizing data in a simple, yet effective manner, offering hints for creative AI utilization beyond predefined uses.
	/// </summary>
	public class ListItem : NotifyPropertyChanged
	{

		/// <summary>
		/// The unique identifier or name for the list item.
		/// </summary>
		public string Key { get => _Key; set => SetProperty(ref _Key, value); }
		string _Key;

		/// <summary>
		/// The value associated with the key.
		/// </summary>
		public string Value { get => _Value; set => SetProperty(ref _Value, value); }
		string _Value;

		/// <summary>
		/// Optional. Provides additional information about the list item,
		/// such as task status (e.g., complete, pending) or extra details for settings and environment variables.
		/// It encourages to infer potential statuses or metadata that could enhance data handling or task management strategies creatively.
		/// </summary>
		public string Comment { get => _Comment; set => SetProperty(ref _Comment, value); }
		string _Comment;
	}
}
