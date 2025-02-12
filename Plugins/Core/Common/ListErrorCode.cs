using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Represents error codes for list operations.
	/// </summary>
	public enum ListErrorCode : int
	{
		/// <summary>Operation successful</summary>
		[Description("Operation successful")]
		Success = 0,

		/// <summary>List not found</summary>
		[Description("List not found")]
		ListNotFound = -1,

		/// <summary>List is read-only</summary>
		[Description("List is read-only")]
		ListReadOnly = -2,

		/// <summary>List already exists</summary>
		[Description("List already exists")]
		ListAlreadyExists = -3,

		/// <summary>List item not found</summary>
		[Description("List item not found")]
		ListItemNotFound = -4
	}
}
