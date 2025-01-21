namespace JocysCom.VS.AiCompanion.Engine.Settings
{
	/// <summary>
	/// Represents a specific setting block and its associated update instructions.
	/// This class is designed to be format-agnostic, so it can be used for XML, JSON, YAML, etc.
	/// </summary>
	public class SettingBlock
	{
		/// <summary>
		/// A universal path that identifies the setting.
		/// For XML, consider using an XPath-like notation, such as "/Settings/Key/SubKey".
		/// For JSON, you might use a dot-notation like "Settings.Key.SubKey".
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// The instructions indicating how this setting block
		/// should be handled during updates.
		/// </summary>
		public UpdateInstruction Instruction { get; set; }
	}
}
