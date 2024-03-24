using System;

namespace JocysCom.VS.AiCompanion.DataClient
{

	[Flags]
	public enum FilePartGroup
	{
		None = 0,
		Group01 = 1,
		Group02 = 2,
		Group03 = 4,
		Group04 = 8,
		Group05 = 16,
		Group06 = 32,
		Group07 = 64,
	}
}
