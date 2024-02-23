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
		/// <summary>Current active open document.</summary>
		[Description("Current Document"), JsonPropertyOrder(3)]
		CurrentDocument = 4,
		/// <summary>Open documents.</summary>
		[Description("Open Documents"), JsonPropertyOrder(4)]
		OpenDocuments = 8192,
		/// <summary>Selected documents in Solution Explorer.</summary>
		[Description("Selected Documents"), JsonPropertyOrder(5)]
		SelectedDocuments = 8,
		/// <summary>Current Project of currently active document.</summary>
		[Description("Current Project"), JsonPropertyOrder(6)]
		CurrentProject = 16,
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
