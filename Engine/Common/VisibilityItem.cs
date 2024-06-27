namespace JocysCom.VS.AiCompanion.Engine
{
	public class VisibilityItem
	{
		/// <summary>
		/// Full path to control.
		/// For example: "OptionsPanel.MainPanel.AlwaysOnToCheckBox"
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// // Enum value: Visible, Collapsed, ReadOnly, etc.
		/// </summary>
		public VisibilityState State { get; set; }
	}
}
