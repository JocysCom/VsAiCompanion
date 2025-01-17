using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Config item.
	/// </summary>
	public class ConfigItem
	{
		/// <summary>
		/// Unique identifier for this item.
		/// </summary>
		[DefaultValue(null)]
		public Guid? Id { get; set; }

		/// <summary>
		/// Identifier of the parent item, applicable in hierarchical structures.
		/// </summary>
		[DefaultValue(null)]
		public Guid? ParentId { get; set; }

		/// <summary>
		/// Logical grouping or type classification for the item.
		/// </summary>
		[DefaultValue(null)]
		public string Category { get; set; }

		/// <summary>
		/// Human-readable label that clarifies the item's purpose or content.
		/// </summary>
		[DefaultValue(null)]
		public string Key { get; set; }

		/// <summary>
		/// Defines how the value should be interpreted or managed.
		/// </summary>
		[DefaultValue(null)]
		public string DataType { get; set; }

		/// <summary>
		/// Actual content or data associated with the item.
		/// </summary>
		[DefaultValue(null)]
		public string Value { get; set; }

		/// <summary>
		/// In-depth explanation or notes about the item.
		/// </summary>
		[DefaultValue(null)]
		public string Description { get; set; }

		/// <summary>
		/// Words or phrases that facilitate searching or categorization.
		/// </summary>
		[DefaultValue(null)]
		public string Tags { get; set; }

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
	}
}
