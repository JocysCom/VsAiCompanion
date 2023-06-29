using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	[Flags]
	public enum AttachmentType
	{
		[Description("")]
		None = 0,
		[Description("Cipboard")]
		Clipboard = 1,
		[Description("Selection")]
		Selection = 2,
		[Description("Active Document")]
		ActiveDocument = 4,
		[Description("Selected Documents")]
		SelectedDocuments = 8,
		[Description("Active Project")]
		ActiveProject = 16,
		[Description("Selected Project")]
		SelectedProject = 32,
		[Description("Solution")]
		Solution = 64,
		[Description("Selected Error")]
		SelectedError = 128,
		[Description("Chat History")]
		ChatHistory = 256,
		[Description("Error")]
		Error = 512,
		[Description("Error Document")]
		ErrorDocument = 1024,
		[Description("Exception")]
		Exception = 2048,
		[Description("Exception Documents")]
		ExceptionDocuments = 4096,
	}

}
