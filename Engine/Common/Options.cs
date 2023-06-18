using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class Options : INotifyPropertyChanged
	{
		public Options()
		{
			ResetPropertiesToDefault(this, false);
		}

		/// <summary>
		/// Avoid deserialization duplicates by using separate method.
		/// </summary>
		public void InitDefaults(bool onlyIfNull = false)
		{
			ResetPropertiesToDefault(this, onlyIfNull);
		}

		#region Helper Methods

		/// <summary>
		/// Assign property values from their [DefaultValueAttribute] value.
		/// </summary>
		/// <param name="o">Object to reset properties on.</param>
		public static void ResetPropertiesToDefault(object o, bool onlyIfNull = false)
		{
			if (o == null)
				return;
			var type = o.GetType();
			var properties = type.GetProperties();
			foreach (var p in properties)
			{
				if (p.CanRead && onlyIfNull && p.GetValue(o, null) != null)
					continue;
				if (!p.CanWrite)
					continue;
				var da = p.GetCustomAttributes(typeof(DefaultValueAttribute), false);
				if (da.Length == 0)
					continue;
				var value = ((DefaultValueAttribute)da[0]).Value;
				p.SetValue(o, value, null);
			}
		}

		#endregion

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if (handler != null)
			{
				if (ControlsHelper.MainTaskScheduler == null)
					handler(this, new PropertyChangedEventArgs(propertyName));
				else
					ControlsHelper.Invoke(handler, this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#endregion


	}
}
