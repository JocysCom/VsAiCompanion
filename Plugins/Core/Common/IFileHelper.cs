namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// File Helper.
	/// </summary>
	public interface IFileHelper
	{
		/// <summary>
		/// Write file text content on user computer.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="contents">The string to write to the file.</param>
		/// <param name="line">Line to start writing.</param>
		/// <param name="column">Column to start writing.</param>
		/// <param name="mode">Write mode, insert, overwrite.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		bool WriteTextFile(string path, string contents, long line, long column, string mode);

	}
}
