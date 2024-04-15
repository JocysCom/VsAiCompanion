using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.DataClient.Common
{

	[Flags]
	public enum EmbeddingGroupFlag
	{
		[Description("None")] None = 0,
		[Description("Group 1")] Group01 = 1 << 0,
		[Description("Group 2")] Group02 = 1 << 1,
		[Description("Group 3")] Group03 = 1 << 2,
		[Description("Group 4")] Group04 = 1 << 3,
		[Description("Group 5")] Group05 = 1 << 4,
		[Description("Group 6")] Group06 = 1 << 5,
		[Description("Group 7")] Group07 = 1 << 6,
		[Description("Group 8")] Group08 = 1 << 7,
		[Description("Group 9")] Group09 = 1 << 8,
		[Description("Group 10")] Group10 = 1 << 9,
		[Description("Group 11")] Group11 = 1 << 10,
		[Description("Group 12")] Group12 = 1 << 11,
		[Description("Group 13")] Group13 = 1 << 12,
		[Description("Group 14")] Group14 = 1 << 13,
		[Description("Group 15")] Group15 = 1 << 14,
		[Description("Group 16")] Group16 = 1 << 15,
		[Description("Group 17")] Group17 = 1 << 16,
		[Description("Group 18")] Group18 = 1 << 17,
		[Description("Group 19")] Group19 = 1 << 18,
		[Description("Group 20")] Group20 = 1 << 19,
		[Description("Group 21")] Group21 = 1 << 20,
		[Description("Group 22")] Group22 = 1 << 21,
		[Description("Group 23")] Group23 = 1 << 22,
		[Description("Group 24")] Group24 = 1 << 23,
		[Description("Group 25")] Group25 = 1 << 24,
		[Description("Group 26")] Group26 = 1 << 25,
		[Description("Group 27")] Group27 = 1 << 26,
		[Description("Group 28")] Group28 = 1 << 27,
		[Description("Group 29")] Group29 = 1 << 28,
		[Description("Group 30")] Group30 = 1 << 29,
		[Description("Group 31")] Group31 = 1 << 30,
		[Description("Group 32")] Group32 = 1 << 31,
	}
}
