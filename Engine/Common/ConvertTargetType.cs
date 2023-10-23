using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
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
		Excel,
		[Description("RTF")]
		RTF,
	}
}
