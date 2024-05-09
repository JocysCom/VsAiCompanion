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

		/// <summary>
		/// Captures a snapshot from the default camera and saves it to a temporary file as a JPG.
		/// This function is designed to allow AI assistants to visually analyze real-time data,
		/// such as identifying objects held by a user. Returns the file path of the captured image.
		/// </summary>
		/// <returns>Path to the JPG file containing the captured image.</returns>
		/// <exception cref="Exception">Thrown when the image capture fails.</exception>
		[RiskLevel(RiskLevel.Low)]
		public async Task<OperationResult<string>> CaptureCameraImage()
		{
			return await CaptureCameraImageCallback();
		}

		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<Task<OperationResult<string>>> CaptureCameraImageCallback { get; set; }

	}
}
