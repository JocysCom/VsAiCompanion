using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Context type.
	/// </summary>
	[Flags]
	public enum ContextType
	{
		/// <summary>None</summary>
		[Description(""), JsonPropertyOrder(0)]
		None = 0,
		/// <summary>Clipboard.</summary>
		[Description("Cipboard"), JsonPropertyOrder(1)]
		Clipboard = 1,
		/// <summary>Selection.</summary>
		[Description("Selection"), JsonPropertyOrder(2)]
		Selection = 2,
		/// <summary>Active open document.</summary>
		[Description("Open Active Document"), JsonPropertyOrder(3)]
		ActiveDocument = 4,
		/// <summary>Open documents.</summary>
		[Description("Open Documents"), JsonPropertyOrder(4)]
		OpenDocuments = 8192,
		/// <summary>Selected documents in Solution Explorer.</summary>
		[Description("Selected Documents"), JsonPropertyOrder(5)]
		SelectedDocuments = 8,
		/// <summary>Active Project of active document.</summary>
		[Description("Active Project"), JsonPropertyOrder(6)]
		ActiveProject = 16,
		/// <summary>Selected Project of selected document in Solution Explorer.</summary>
		[Description("Selected Project"), JsonPropertyOrder(7)]
		SelectedProject = 32,
		/// <summary>Solution.</summary>
		[Description("Solution"), JsonPropertyOrder(8)]
		Solution = 64,
		/// <summary>Chat history.</summary>
		[Description("Chat History"), JsonPropertyOrder(9)]
		ChatHistory = 256,
		/// <summary>Error info.</summary>
		[Description("Error"), JsonPropertyOrder(10)]
		Error = 512,
		/// <summary>Document related to the error.</summary>
		[Description("Error Document"), JsonPropertyOrder(11)]
		ErrorDocument = 1024,
		/// <summary>Exception info.</summary>
		[Description("Exception"), JsonPropertyOrder(12)]
		Exception = 2048,
		/// <summary>Documents related to exception</summary>
		[Description("Exception Documents"), JsonPropertyOrder(13)]
		ExceptionDocuments = 4096,
	}

}
