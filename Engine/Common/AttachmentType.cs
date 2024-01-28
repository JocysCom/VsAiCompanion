using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	[Flags]
	public enum AttachmentType
	{
		[Description(""), JsonPropertyOrder(0)]
		None = 0,
		[Description("Cipboard"), JsonPropertyOrder(1)]
		Clipboard = 1,
		[Description("Selection"), JsonPropertyOrder(2)]
		Selection = 2,
		[Description("Open Active Document"), JsonPropertyOrder(3)]
		ActiveDocument = 4,
		[Description("Open Documents"), JsonPropertyOrder(4)]
		OpenDocuments = 8192,
		[Description("Selected Documents"), JsonPropertyOrder(5)]
		SelectedDocuments = 8,
		[Description("Active Project"), JsonPropertyOrder(6)]
		ActiveProject = 16,
		[Description("Selected Project"), JsonPropertyOrder(7)]
		SelectedProject = 32,
		[Description("Solution"), JsonPropertyOrder(8)]
		Solution = 64,
		//[Description("Selected Error")]	
		//SelectedError = 128,
		[Description("Chat History"), JsonPropertyOrder(9)]
		ChatHistory = 256,
		[Description("Error"), JsonPropertyOrder(10)]
		Error = 512,
		[Description("Error Document"), JsonPropertyOrder(11)]
		ErrorDocument = 1024,
		[Description("Exception"), JsonPropertyOrder(12)]
		Exception = 2048,
		[Description("Exception Documents"), JsonPropertyOrder(13)]
		ExceptionDocuments = 4096,
	}

}
