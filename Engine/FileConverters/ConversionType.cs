using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	public enum ConversionType
	{
		[Description("Convert:")]
		None = 0,
		[Description("JSON to RTF")]
		JSON2RTF,
		[Description("JSON to CSV")]
		JSON2CSV,
		[Description("JSON to XLS")]
		JSON2XLS,
		[Description("RTF to JSON")]
		RTF2JSON,
		[Description("CSV to JSON")]
		CSV2JSON,
		[Description("XLS to JSON")]
		XLS2JSON,
		[Description("JSON to JSONL")]
		JSON2JSONL,
		[Description("JSONL to JSON")]
		JSONL2JSON,
	}
}
