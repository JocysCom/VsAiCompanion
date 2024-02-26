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
		/// <param name="mode">Write mode: 'insert' or `overwrite`.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		bool WriteTextFile(string path, string contents, long line, long column, string mode);

		/// <summary>
		/// Read file text content from user computer.
		/// </summary>
		/// <param name="path">The file to read from.</param>
		/// <param name="line">Line to start reading.</param>
		/// <param name="column">Column to start reading.</param>
		/// <param name="length">Read amount.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		string ReadTextFile(string path, long line, long column, long length);


	}
}
