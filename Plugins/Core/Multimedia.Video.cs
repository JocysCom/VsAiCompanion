using JocysCom.ClassLibrary;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
		/// Send all the pictures at once if they belong to one document.
		/// </summary>
		/// <param name="instructions">Guidelines for AI to follow during image analysis.</param>
		/// <param name="pathsOrUrls">Paths to local files or URLs to images for analysis. Supported image file types: .jpg, .png, .gif, .bmp, .tiff</param>
		/// <returns>Analysis results.</returns>
		[RiskLevel(RiskLevel.Low)]
		public async Task<OperationResult<string>> AnalysePictures(
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

		#region Convert to PDF and Image


		/// <summary>
		/// Convert file to PDF.
		/// </summary>
		/// <param name="inputFilePath">Source file.</param>
		/// <param name="outputFilePath">Target PDF File.</param>
		/// <returns>True if the operation was successful.</returns>
		//[RiskLevel(RiskLevel.High)]
		public static OperationResult<bool> ConvertToPDF(string inputFilePath, string outputFilePath)
		{
			try
			{
				// Register the printer's output file path via the registry
				SetDefaultPrinterOutput(outputFilePath);

				// Create a new process to print the document
				ProcessStartInfo processInfo = new ProcessStartInfo()
				{
					Verb = "print",
					FileName = inputFilePath,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					// Specify the printer: Microsoft Print to PDF
					Arguments = "\"Microsoft Print to PDF\""
				};

				Process process = new Process()
				{
					StartInfo = processInfo
				};
				process.Start();
				process.WaitForExit();

				return new OperationResult<bool>(true);
			}
			catch (System.Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		private static void SetDefaultPrinterOutput(string outputFilePath)
		{
			// Define the registry path for the printer
			string registryPath = @"Software\Microsoft\Windows NT\CurrentVersion\Print\Printers\Microsoft Print to PDF\PrinterDriverData";

			// Try to open the registry key
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath, true))
			{
				if (key == null)
				{
					throw new System.Exception($"Registry path not found or inaccessible: {registryPath}");
				}

				// Set the default output file
				key.SetValue("OutputFile", outputFilePath, RegistryValueKind.String);
			}
		}

		/// <summary>
		/// Convert PDF to Image.
		/// </summary>
		/// <param name="pdfFilePath">Source PDF file.</param>
		/// <param name="outputFolder">Target folder for JPG Files.</param>
		/// <param name="pages">Optional. The zero-based index array of page numbers to convert. If null or empty, all pages are converted.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		public static OperationResult<List<string>> ConvertPdfToImage(
			string pdfFilePath, string outputFolder,
			int[] pages = null
		)
		{
			var pdfImageFiles = new List<string>();
			try
			{
				var pdfFi = new FileInfo(pdfFilePath);
				var pdf = File.ReadAllBytes(pdfFilePath);
				var pageImages = pages is null || !pages.Any()
					? PDFtoImage.Conversion.ToImages(pdf)
					: PDFtoImage.Conversion.ToImages(pdf, pages.AsEnumerable());
				var totalPageCount = pageImages.Count();
				var maxImageCount = 25d;
				var maxSize = (int)Math.Ceiling(totalPageCount / maxImageCount);
				var pageImageGroups = new List<List<SKBitmap>>();

				for (int i = 0; i < totalPageCount; i += maxSize)
				{
					var pageImageGroup = pageImages.Skip(i).Take(maxSize).ToList();
					pageImageGroups.Add(pageImageGroup);
				}

				if (!Directory.Exists(outputFolder))
					Directory.CreateDirectory(outputFolder);

				var count = 0;
				var pdfImageName = string.Empty;
				foreach (var pageImageGroup in pageImageGroups)
				{
					pdfImageName = Path.Combine(outputFolder, $"{pdfFi.Name}.Part_{count}.jpg");
					var totalHeight = pageImageGroup.Sum(image => image.Height);
					var width = pageImageGroup.Max(image => image.Width);
					var stitchedImage = new SKBitmap(width, totalHeight);
					var canvas = new SKCanvas(stitchedImage);
					var currentHeight = 0;

					foreach (var pageImage in pageImageGroup)
					{
						canvas.DrawBitmap(pageImage, 0, currentHeight);
						currentHeight += pageImage.Height;
					}

					using (var stitchedFileStream = new FileStream(pdfImageName, FileMode.Create, FileAccess.Write))
					{
						stitchedImage.Encode(stitchedFileStream, SKEncodedImageFormat.Jpeg, 100);
					}

					pdfImageFiles.Add(pdfImageName);
					count++;
					Console.WriteLine();
				}
				var result = new OperationResult<List<string>>(pdfImageFiles);
				result.StatusText = $"Saved image to {pdfImageName}";
				return result;
			}
			catch (Exception ex)
			{
				return new OperationResult<List<string>>(ex);
			}
		}

		/// <summary>
		/// Will be used by plugins manager.
		/// </summary>
		public Func<string> GetStructuredImageAnalysisInstructions { get; set; }

		/// <summary>
		/// Converts a PDF file to structured JSON format, with an option to specify a page range.
		/// </summary>
		/// <param name="pdfFilePath">The path to the source PDF file.</param>
		/// <param name="pages">Optional. The zero-based index array of page numbers to convert. If null or empty, all pages are converted.</param>
		/// <returns>An OperationResult containing a list of structured JSON strings.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public async Task<OperationResult<string>> ConvertPdfToStructuredJson(string pdfFilePath, int[] pages = null)
		{
			try
			{
				var fi = new FileInfo(pdfFilePath);
				var tempName = $"{fi.Name}_{System.IO.Path.GetRandomFileName()}";
				var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AICOMP", tempName);
				var tempDi = new DirectoryInfo(tempPath);
				if (!tempDi.Exists)
					tempDi.Create();
				var convertResult = ConvertPdfToImage(pdfFilePath, tempDi.FullName, pages);
				if (!convertResult.Success)
					return convertResult.ToResult<string>(null);
				var instructions = GetStructuredImageAnalysisInstructions();
				var analyseResult = await AnalysePictures(instructions, convertResult.Data.ToArray());
				// Cleanup
				tempDi.Delete(true);
				return analyseResult;
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		#endregion

	}
}
