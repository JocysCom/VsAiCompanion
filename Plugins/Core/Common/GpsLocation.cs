using System;
using System.Threading.Tasks;

#if NETFRAMEWORK
using System.Device.Location;
#else
//using Windows.Devices.Geolocation;
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
		/// Windows 11: Location Privacy Setings -> [x] Let desktop apps access your location.
		/// </summary>
		public static async Task<(double? altitude, double? latitude, double? longitude)> GetCurrentLocation()
		{
			double? altitude = null;
			double? latitude = null;
			double? longitude = null;
			await Task.Delay(0);

#if NETFRAMEWORK
			var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High);
			// Wait 1000 milliseconds to start.
			var success = watcher.TryStart(false, TimeSpan.FromMilliseconds(1000));
			var location = watcher.Position.Location;
			altitude = location?.Altitude;
			latitude = location?.Latitude;
			longitude = location?.Longitude;
#else
			//var accessStatus = await Geolocator.RequestAccessAsync();

			//if (accessStatus == GeolocationAccessStatus.Allowed)
			//{
			//	var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.High };
			//	var pos = await geolocator.GetGeopositionAsync();
			//	var coord = pos.Coordinate;

			//	altitude = coord.Point.Position.Altitude;
			//	latitude = coord.Point.Position.Latitude;
			//	longitude = coord.Point.Position.Longitude;
			//}

#endif
			return (altitude, latitude, longitude);
		}
	}

}
