using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Represents a versatile list item used in various contexts such as TODO lists, environment variable management, and diverse list compilations.
	/// Structured as a key-value pair with an optional comment, facilitating a broad spectrum of applications from settings configuration to task management.
	/// Aides in organizing data in a simple, yet effective manner, offering hints for creative AI utilization beyond predefined uses.
	/// </summary>
	public class ListItem : NotifyPropertyChanged
	{

		/*
		/// <summary>
		/// Unique identifier for this item.
		/// </summary>
		[DefaultValue(null)]
		public System.Guid? Id { get; set; }

		/// <summary>
		/// Identifier of the parent item, applicable in hierarchical structures.
		/// </summary>
		[DefaultValue(null)]
		public System.Guid? ParentId { get; set; }

		/// <summary>
		/// Logical grouping or type classification for the item.
		/// </summary>
		[DefaultValue(null)]
		public string Category { get; set; }

		/// <summary>
		/// Defines how the value should be interpreted or managed.
		/// </summary>
		[DefaultValue(null)]
		public string DataType { get; set; }
		*/

		/// <summary>
		/// Optional. List item progress status.
		/// </summary>
		[DefaultValue(null)]
		public ProgressStatus? Status { get => _Status; set => SetProperty(ref _Status, value); }
		ProgressStatus? _Status;

		/// <summary>XML serializer should serialize the Status property only if it is not null</summary>
		public bool ShouldSerializeStatus() => Status != null;

		/// <summary>
		/// The unique identifier or name for the list item.
		/// </summary>
		public string Key { get => _Key; set => SetProperty(ref _Key, value); }
		string _Key;

		/// <summary>
		/// Optional. The value associated with the key.
		/// </summary>
		[DefaultValue(null)]
		public string Value { get => _Value; set => SetProperty(ref _Value, value); }
		string _Value;

		/// <summary>
		/// Optional. Provides additional information about the list item,
		/// such as task status (e.g., complete, pending) or extra details for settings and environment variables.
		/// It encourages to infer potential statuses or metadata that could enhance data handling or task management strategies creatively.
		/// </summary>
		[DefaultValue(null)]
		public string Comment { get => _Comment; set => SetProperty(ref _Comment, value); }
		string _Comment;

		/// <summary>
		/// Words or phrases that facilitate searching or categorization.
		/// </summary>
		[DefaultValue(null)]
		public string Tags { get => _Tags; set => SetProperty(ref _Tags, value); }
		string _Tags;

		/*
		/// <summary>
		/// In-depth explanation or notes about the item.
		/// </summary>
		[DefaultValue(null)]
		public string Description { get; set; }

		/// <summary>
		/// Indicates whether the item is active or being utilized.
		/// </summary>
		[DefaultValue(null)]
		public bool? Enabled { get; set; }

		/// <summary>
		/// Timestamp marking when the item was originally added.
		/// </summary>
		[DefaultValue(null)]
		public DateTime? CreateDate { get; set; }

		/// <summary>
		/// Timestamp reflecting when the item was last updated.
		/// </summary>
		[DefaultValue(null)]
		public DateTime? UpdateDate { get; set; }
		*/
	}
}
