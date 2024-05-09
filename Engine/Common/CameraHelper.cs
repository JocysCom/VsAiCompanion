using Emgu.CV;
using JocysCom.ClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class CameraHelper
	{

		/// <summary>
		///  Capture image from the default camera.
		/// </summary>
		public static async Task<OperationResult<string>> CaptureCameraImage()
		{
			try
			{
				Global.MainControl.Dispatcher.Invoke(() =>
				{

				});

				// Initialize the capture from the default camera
				using (var capture = new VideoCapture(0, VideoCapture.API.Any))
				{
					// Give the camera a moment to initialize
					await Task.Delay(500);
					// Capture a frame from the camera
					using (var image = capture.QueryFrame())
					{
						if (image != null)
						{
							var name = $"Camera_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
							var path = System.IO.Path.Combine(Global.AppData.XmlFile.Directory.FullName, "Temp", "Camera");
							var imagePath = System.IO.Path.Combine(path, name + ".jpg");
							var fi = new FileInfo(imagePath);
							fi.Directory.Create();
							// Save the image to the file as JPG
							image.Save(imagePath);
							// Return the path to the saved image
							return new OperationResult<string>(imagePath);
						}
						else
						{
							return new OperationResult<string>(new Exception("Failed to capture image from camera."));
						}
					}
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}

		}

	}
}
