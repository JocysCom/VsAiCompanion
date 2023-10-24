using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	public enum ConvertTargetType

	{
		[Description("Convert To:")]
		None = 0,
		[Description("JSON")]
		JSON,
		[Description("JSON Lines")]
		JSONL,
		[Description("Excel")]
		XLS,
		[Description("Rich Text Format (*.rtf)")]
		RTF,
		[Description("CSV")]
		CSV,
		[Description("Word Document (*.docx)")]
		DOCX,
	}
}
