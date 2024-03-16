namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	///  Command line arguments used when program have to run as an administrator.
	/// </summary>
	public enum AdminCommand
	{
		/// <summary>
		/// Renames the application executable during an update if the app is located inside a protected folder.
		/// </summary>
		UpdaterRenameFiles,
		/// <summary>
		/// Helps to restart the app if only one instance is allowed.
		/// </summary>
		UpdaterRestartApp,
	}
}
