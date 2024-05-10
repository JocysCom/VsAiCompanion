using Emgu.CV;
using Hompus.VideoInputDevices;
using JocysCom.ClassLibrary;
using System;
using System.IO;
using System.Linq;
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
				int index;
				using (var sde = new SystemDeviceEnumerator())
				{
					var devices = sde.ListVideoInputDevice();
					index = devices.FirstOrDefault(d => d.Value == Global.AppSettings.VideoInputDevice).Key;
				}
				// Initialize the capture from the default camera
				using (var capture = new VideoCapture(index, VideoCapture.API.DShow))
				{
					// Ensure the captured image is automatically converted to RGB
					capture.Set(Emgu.CV.CvEnum.CapProp.ConvertRgb, 1);
					// Give the camera a moment to initialize
					await Task.Delay(2000);
					await Task.Delay(500);
					// Capture a frame from the camera
					capture.Grab();
					Mat image = new Mat();
					bool success = capture.Read(image);
					if (success && !image.IsEmpty)
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
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}


	}
}
