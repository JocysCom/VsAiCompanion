using JocysCom.ClassLibrary;
using System;
using System.Drawing;
using System.Drawing.Imaging;
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
		/// Analyzes visual content (pictures/photos) based on given instructions using an AI model.
		/// Supported file types: .jpg, .png, .gif, .bmp, .tiff
		/// Do not use for analyzing plain text files.
		/// </summary>
		/// <param name="instructions">Guidelines for AI to follow during image analysis.</param>
		/// <param name="pathsOrUrls">Paths to local files or URLs to images for analysis. Supported image file types: .jpg, .png, .gif, .bmp, .tiff</param>
		/// <returns>Analysis results.</returns>
		[RiskLevel(RiskLevel.Low)]
		public async Task<OperationResult<string>> AnalysePicture(
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
		/// <returns>Path to the file containing the captured image.</returns>
		/// <exception cref="Exception">Thrown when the image capture fails.</exception>
		//[RiskLevel(RiskLevel.Low)] // Disable until better library found.
		public async Task<OperationResult<string>> CaptureCameraImage()
		{
			return await CaptureCameraImageCallback();
		}

		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<Task<OperationResult<string>>> CaptureCameraImageCallback { get; set; }


		/// <summary>
		/// Capture an image of the screen, window or a defined region based on supplied parameters.
		/// If no parameters are provided, default to capturing a user-defined region.
		/// </summary>
		/// <param name="screenId">Screen ID to capture. If provided, it will capture the specific screen.</param>
		/// <param name="windowName">Window name to capture. If provided, it will capture the specified window.</param>
		/// <param name="region">Region to capture. If provided, it will capture the specified region.</param>
		/// <param name="imageFolder">Folder path to save the captured image. If not provided, it defaults to the temp directory.</param>
		/// <param name="format">Image format for the captured image. If not provided, it defaults to PNG.</param>
		/// <returns>Operation result containing the path to the captured image.</returns>
		/// <exception cref="ArgumentException">Thrown if invalid parameters are supplied.</exception>
		[RiskLevel(RiskLevel.Low)]
		public async static Task<OperationResult<string>> CaptureImage(
			int? screenId = null,
			string windowName = null,
			Rectangle? region = null,
			string imageFolder = null,
			ImageFormat format = null)
		{
			// Validate parameters
			if (screenId.HasValue && !string.IsNullOrEmpty(windowName))
				return new OperationResult<string>(new ArgumentException("Both screenId and windowName cannot be provided simultaneously."));
			if (screenId.HasValue && region.HasValue)
				return new OperationResult<string>(new ArgumentException("Both screenId and region cannot be provided simultaneously."));
			if (!string.IsNullOrEmpty(windowName) && region.HasValue)
				return new OperationResult<string>(new ArgumentException("Both windowName and region cannot be provided simultaneously."));
			// Determine the task to execute
			if (screenId.HasValue)
			{
				return await ScreenshotHelper.CaptureScreen(screenId, imageFolder, format);
			}
			else if (!string.IsNullOrEmpty(windowName))
			{
				return await ScreenshotHelper.CaptureWindow(windowName, imageFolder, format);
			}
			else if (region.HasValue)
			{
				return await ScreenshotHelper.CaptureRegion(region, imageFolder, format);
			}
			else
			{
				return await ScreenshotHelper.CaptureUserDefinedRegion(imageFolder, format);
			}
		}
	}
}
