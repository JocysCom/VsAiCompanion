using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Configuration
{
	public class SettingsListFileItem : SettingsFileItem, ISettingsListFileItem
	{
		[DefaultValue(false)]

		public bool IsChecked { get => _IsChecked; set => SetProperty(ref _IsChecked, value); }
		bool _IsChecked;

		[DefaultValue(null)]
		public string StatusText { get => _StatusText; set => SetProperty(ref _StatusText, value); }
		string _StatusText;

		[DefaultValue(MessageBoxImage.None)]
		public System.Windows.MessageBoxImage StatusCode { get => _StatusCode; set => SetProperty(ref _StatusCode, value); }
		System.Windows.MessageBoxImage _StatusCode;

		[XmlIgnore, JsonIgnore]
		public DrawingImage Icon
		{
			get
			{
				if (_Icon == null || !string.IsNullOrEmpty(IconData))
				{
					var svgContent = GetContent(IconData);
					_Icon = ConvertToImage(svgContent);
				}
				return _Icon;
			}
		}
		DrawingImage _Icon;

		[DefaultValue(null)]
		public string IconType { get => _IconType; set => SetProperty(ref _IconType, value); }
		string _IconType;

		[DefaultValue(null)]
		public string IconData { get => _IconData; set => SetProperty(ref _IconData, value); }
		string _IconData;

		public void SetIcon(string contents, string type = ".svg")
		{
			IconType = type;
			IconData = GetBase64(contents);
		}

		public static string GetBase64(string content)
		{

			if (string.IsNullOrEmpty(content))
				return null;
			var bytes = Encoding.UTF8.GetBytes(content);
			var compressed = JocysCom.ClassLibrary.Configuration.SettingsHelper.Compress(bytes);
			var base64 = System.Convert.ToBase64String(compressed, System.Base64FormattingOptions.InsertLineBreaks);
			return base64;
		}

		public static string GetContent(string base64)
		{
			if (string.IsNullOrEmpty(base64))
				return null;
			var bytes = System.Convert.FromBase64String(base64);
			var decompressed = JocysCom.ClassLibrary.Configuration.SettingsHelper.Decompress(bytes);
			var content = Encoding.UTF8.GetString(decompressed);
			return content;
		}

		public static Func<string, DrawingImage> ConvertToImage;

		#region Lists grouping

		public DateTime Created { get => _Created; set => SetProperty(ref _Created, value); }
		DateTime _Created;

		/// <summary>
		/// Computed property for group name
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string ListGroupTime
		{
			get
			{
				var today = DateTime.Today;
				var yesterday = today.AddDays(-1);
				if (Created.Date == today) return "Today";
				else if (Created.Date == yesterday) return "Yesterday";
				else if (Created.Year == today.Year) return Created.ToString("MMMM"); // Month name if the same year
				else return Created.Year.ToString(); // Just the year if different year
			}
		}

		[XmlIgnore, JsonIgnore]
		public string ListGroupPath
			=> string.IsNullOrEmpty(Path) ? "Main" : Path;

		[XmlIgnore, JsonIgnore]
		public string ListGroupName
		{
			get
			{
				var groups = (Name ?? "").Split('-');
				return groups.Length == 0 ? "Main" : groups[0];
			}
		}

		#endregion

		#region ■ INotifyPropertyChanged

		protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			if (propertyName == nameof(IconData))
			{
				var svgContent = GetContent(IconData);
				_Icon = ConvertToImage?.Invoke(svgContent);
			}
			base.OnPropertyChanged(propertyName);
			if (propertyName == nameof(IconData))
				OnPropertyChanged(nameof(Icon));
		}

		#endregion

	}
}
