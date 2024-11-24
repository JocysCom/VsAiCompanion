using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Items in the list with uniue id.
	/// </summary>
	public interface IHasGuid
	{
		Guid Id { get; set; }
	}
}
