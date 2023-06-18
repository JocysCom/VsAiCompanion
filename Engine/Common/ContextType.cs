using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	[Flags]
	public enum AttachmentType

	{
		None = 0,
		Clipboard = 1,
		Selection = 2,
		ActiveDocument = 4,
		SelectedDocuments = 8,
		ActiveProject = 16,
		SelectedProject = 32,
		Solution = 64,
		SelectedError = 128,
		ChatHistory = 256,
	}

}
