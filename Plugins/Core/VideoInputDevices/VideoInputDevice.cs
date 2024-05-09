using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hompus.VideoInputDevices
{
	/// <summary>
	/// A video input device that is detected in the system.
	/// </summary>
	public class VideoInputDevice
	{
		/// <summary>
		/// The name of the video input device.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="VideoInputDevice"/> class.
		/// </summary>
		/// <param name="moniker">A moniker object.</param>
		public VideoInputDevice(IMoniker moniker)
		{
			this.Name = this.GetFriendlyName(moniker);
		}

		/// <summary>
		/// Get the name represented by the moniker.
		/// </summary>
		private string GetFriendlyName(IMoniker moniker)
		{
			object bagObject = null;

			try
			{
				var bagId = typeof(IPropertyBag).GUID;

				// Get property bag of the moniker
				moniker.BindToStorage(null, null, ref bagId, out bagObject);
				var propertyBag = (IPropertyBag)bagObject;

				// Read FriendlyName
				object value = null;
				var hresult = propertyBag.Read("FriendlyName", ref value, IntPtr.Zero);
				if (hresult != 0)
				{
					Marshal.ThrowExceptionForHR(hresult);
				}

				return value as string ?? string.Empty;
			}
			catch (Exception)
			{
				return string.Empty;
			}
			finally
			{
				if (bagObject != null)
				{
					Marshal.ReleaseComObject(bagObject);
					bagObject = null;
				}
			}
		}
	}
}
