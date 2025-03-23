using System;

namespace JocysCom.VS.AiCompanion.Engine.Settings
{
	/// <summary>
	/// Represents the current state of an item that might be subject to reset operations.
	/// </summary>
	[Flags]
	public enum ItemState
	{
		None = 0,
		/// <summary>Item exists in its original state</summary>
		Original = 1 << 0,
		/// <summary>Item has been modified by the user</summary>
		Modified = 1 << 1,
		/// <summary>Item has been deleted by the user</summary>
		Deleted = 1 << 2,
		/// <summary>Item was created by the user (not from defaults)</summary>
		UserCreated = 1 << 3
	}
}
