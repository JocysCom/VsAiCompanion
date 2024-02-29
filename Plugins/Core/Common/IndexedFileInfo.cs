using System;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Represents the metadata of a file indexed by the Windows Search.
	/// </summary>
	public class IndexedFileInfo
	{
		/// <summary>
		/// Gets or sets the name of the item, typically the file name including the extension.
		/// </summary>
		public string ItemName { get; set; }

		/// <summary>
		/// Gets or sets the full path of the item, suitable for display to the user.
		/// </summary>
		public string ItemPathDisplay { get; set; }

		/// <summary>
		/// Gets or sets a text description of the item type (e.g., "JPEG image").
		/// </summary>
		public string ItemTypeText { get; set; }

		/// <summary>
		/// Gets or sets the date the item was last modified.
		/// </summary>
		public DateTime? DateModified { get; set; }

		/// <summary>
		/// Gets or sets the file extension of the item.
		/// </summary>
		public string FileExtension { get; set; }

		/// <summary>
		/// Gets or sets the size of the item, in bytes.
		/// </summary>
		public long? Size { get; set; }

		/// <summary>
		/// Gets or sets the author of the document. Applicable to documents that store this metadata.
		/// </summary>
		public string Author { get; set; }

		/// <summary>
		/// Gets or sets the title of the document.
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the keywords associated with the file. This can be a collection of keywords.
		/// </summary>
		public IEnumerable<string> Keywords { get; set; }

		/// <summary>
		/// Gets or sets any comment associated with the file.
		/// </summary>
		public string Comment { get; set; }

		/// <summary>
		/// Gets or sets the date the item was created.
		/// </summary>
		public DateTime? DateCreated { get; set; }

		/// <summary>
		/// Gets or sets the date the item was last accessed.
		/// </summary>
		public DateTime? DateAccessed { get; set; }
	}
}
