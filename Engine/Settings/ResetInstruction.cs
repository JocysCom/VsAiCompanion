using System;

namespace JocysCom.VS.AiCompanion.Engine.Settings
{
	/// <summary>
	/// Describes the actions to perform on a setting block when updating the application.
	/// Use combination of flags if multiple behaviors are desired.
	/// </summary>
	[Flags]
	public enum ResetInstruction
	{
		None = 0,
		RestoreIfNotExists = 1 << 0,
		RestoreIfNotExistsOnAppUpdate = 1 << 1,
		RestoreIfNotExistsOnSettingsUpdate = 1 << 2,
		RestoreIfNotDeleted = 1 << 3,

		///// <summary>
		///// Always reset this setting block to default values on app update.
		///// </summary>
		//ResetToDefaultOnAppUpdate = 1 << 1,

		///// <summary>
		///// Merge the default settings with any existing settings.
		///// If a setting key/value pair already exists, keep the existing one; 
		///// otherwise add new or default keys/values.
		///// </summary>
		//MergeWithExisting = 1 << 2,

		///// <summary>
		///// Overwrite any existing settings with default.
		///// Combines well with <c>BackupBeforeOverwrite</c>.
		///// </summary>
		//OverwriteExisting = 1 << 3,

		///// <summary>
		///// Create a backup before overwriting the existing settings.
		///// </summary>
		//BackupBeforeOverwrite = 1 << 4,

		///// <summary>
		///// If the local settings are newer or modified, do not overwrite.
		///// This implies a version check or timestamp logic in your code.
		///// </summary>
		//SkipIfLocalIsNewer = 1 << 5,
	}
}
