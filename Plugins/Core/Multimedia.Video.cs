using JocysCom.ClassLibrary;
using System;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Multimedia, encompassing Text (as symbolic representations), Audio (as pressure waves), and Video (as electromagnetic waves),
	/// should be addressed through unified methods, given the prevalence of files that integrate text, audio, and video components.
	/// </summary>
	public partial class Multimedia
	{
		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<string, string[], Task<OperationResult<string>>> VideoToText { get; set; }

		/// <summary>
		/// Analyzes image files or URLs as per given instructions with an AI model. Can analyse image files on user computer.
		/// </summary>
		/// <param name="instructions">Guidelines for AI to follow during image analysis.</param>
		/// <param name="pathsOrUrls">Paths to local files or URLs to images for analysis.</param>
		/// <returns>Analysis results.</returns>
		[RiskLevel(RiskLevel.Low)]
		public async Task<OperationResult<string>> AnalyseImage(
			string instructions,
			string[] pathsOrUrls
			)
		{
			return await VideoToText(instructions, pathsOrUrls);
		}

	}
}
