using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hompus.VideoInputDevices
{
	/// <summary>
	/// A system device enumerator.
	/// </summary>
	public class SystemDeviceEnumerator : IDisposable
	{
		private bool disposed;
		private ICreateDevEnum _systemDeviceEnumerator;

		/// <summary>
		/// Initializes a new instance of the <see cref="SystemDeviceEnumerator"/> class.
		/// </summary>
		public SystemDeviceEnumerator()
		{
			var comType = Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86"));
			_systemDeviceEnumerator = (ICreateDevEnum)Activator.CreateInstance(comType);
		}

		/// <summary>
		/// Lists the video input devices connected to the system.
		/// </summary>
		/// <returns>A dictionary with the id and name of the device.</returns>
		public IReadOnlyDictionary<int, string> ListVideoInputDevice()
		{
			var videoInputDeviceClass = new Guid("{860BB310-5D01-11D0-BD3B-00A0C911CE86}");
			var list = new Dictionary<int, string>();
			var hresult = _systemDeviceEnumerator.CreateClassEnumerator(ref videoInputDeviceClass, out var enumMoniker, 0);
			// If no devices of the category
			if (hresult != 0)
				return list;
			var moniker = new IMoniker[1];
			while (true)
			{
				hresult = enumMoniker.Next(1, moniker, IntPtr.Zero);
				if (hresult != 0 || moniker[0] == null)
					break;
				var device = new VideoInputDevice(moniker[0]);
				list.Add(list.Count, device.Name);
				// Release COM object
				Marshal.ReleaseComObject(moniker[0]);
				moniker[0] = null;
			}
			return list;
		}

		/// <summary>
		/// rees, releases, or resets unmanaged resources.
		/// </summary>
		/// <param name="disposing"><c>false</c> if invoked by the finalizer because the object is being garbage collected; otherwise, <c>true</c></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;
			if (disposing)
			{
				if (!(_systemDeviceEnumerator is null))
				{
					Marshal.ReleaseComObject(_systemDeviceEnumerator);
					_systemDeviceEnumerator = null;
				}
			}
			disposed = true;
		}

		/// <summary>
		/// Frees, releases, or resets unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
