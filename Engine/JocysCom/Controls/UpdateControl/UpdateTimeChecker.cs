using System;
using System.Linq;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	public static class UpdateTimeChecker
	{
		public static void CheckUpdates(UpdateTimeSettings settings, Func<bool> updateAction, bool forceUpdate = false)
		{
			bool updateSuccess = false;
			if (forceUpdate || ShouldCheckForUpdates(settings))
			{
				try
				{
					// Execute the update action and pass the result back
					updateSuccess = updateAction();
					// Update the settings based on the result
					if (updateSuccess)
					{
						settings.LastUpdate = DateTime.Now;
						Console.WriteLine("Update was successful. Last update time updated.");
					}
					else
					{
						Console.WriteLine("Update failed. Last update time remains unchanged.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Exception occurred during update: {ex.Message}");
				}
			}
			else
			{
				Console.WriteLine("No need to check for updates at this time.");
			}
		}

		public static bool ShouldCheckForUpdates(UpdateTimeSettings settings)
		{
			DateTime nextUpdate = GetNextUpdate(settings);
			if (DateTime.Now >= nextUpdate)
				return true;
			return false;
		}

		public static DateTime GetNextUpdate(UpdateTimeSettings settings)
		{
			return TimeUnitHelper.GetDateTimes(settings.LastUpdate ?? DateTime.MinValue, settings.CheckFrequencyValue, settings.CheckFrequencyUnit).Last();
		}
	}
}
