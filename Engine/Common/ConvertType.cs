using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
    public enum ConvertType
    {
		[Description("Convert To:")]
		None = 0,
		JSON,
		JSONL,
		Excel
    }
}
