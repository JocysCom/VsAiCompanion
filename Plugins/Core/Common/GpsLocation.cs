using System;
using System.Threading.Tasks;

#if NETFRAMEWORK
using System.Device.Location;
#else
#endif


namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// GPS Location.
	/// </summary>
	public class GpsLocation
	{

		/// <summary>
		/// Get current GPS location.
		/// </summary>
		public static async Task<(double? altitude, double? latitude, double? longitude)> GetCurrentLocation()
		{
			double? altitude = null;
			double? latitude = null;
			double? longitude = null;
			await Task.Delay(0);

#if NETFRAMEWORK
			var watcher = new GeoCoordinateWatcher();
			// Wait 1000 milliseconds to start.
			watcher.TryStart(false, TimeSpan.FromMilliseconds(1000));
			var location = watcher.Position.Location;
			altitude = location?.Altitude;
			latitude = location?.Latitude;
			longitude = location?.Longitude;
#else

#endif
			return (altitude, latitude, longitude);
		}
	}

}
