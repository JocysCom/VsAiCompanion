using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Settings
{
	/// <summary>
	/// Example usage demonstrating how to create and store a list of setting blocks.
	/// </summary>
	public static class SettingBlockManager
	{
		/// <summary>
		/// Build a sample list of setting blocks with diverse update instructions.
		/// </summary>
		public static List<SettingBlock> GetDefaultSettingBlocks()
		{
			return new List<SettingBlock>
			{
				new SettingBlock
				{
					Path = "/Settings/Templates/AIChat/Personalized",
					Instruction = UpdateInstruction.RestoreIfNotExists
				},
				new SettingBlock
				{
					Path = "/Settings/List/Prompts/Prompts",
					Instruction = UpdateInstruction.RestoreIfNotExists
								 | UpdateInstruction.ResetToDefaultOnAppUpdate
				},
				new SettingBlock
				{
					Path = "Settings.Advanced.FeatureFlags",
					Instruction = UpdateInstruction.MergeWithExisting
				},
				new SettingBlock
				{
					Path = "/Settings/UserPreferences/Theme",
					Instruction = UpdateInstruction.OverwriteExisting
								  | UpdateInstruction.BackupBeforeOverwrite
				},
				new SettingBlock
				{
					Path = "Settings.VersionInfo",
					Instruction = UpdateInstruction.SkipIfLocalIsNewer
				}
			};
		}
	}
}
