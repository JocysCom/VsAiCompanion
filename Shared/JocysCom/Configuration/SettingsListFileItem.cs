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
		public SettingsListFileItem() : base()
		{
		}

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
				if (_Icon == null && !string.IsNullOrEmpty(IconData))
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

		[DefaultValue(false)]
		public bool IsPinned
		{
			get => _IsPinned;
			set
			{
				SetProperty(ref _IsPinned, value);
				OnPropertyChanged(nameof(ListGroupTime));
				OnPropertyChanged(nameof(ListGroupPath));
				OnPropertyChanged(nameof(ListGroupName));
				OnPropertyChanged(nameof(ListGroupTimeSortKey));
				OnPropertyChanged(nameof(ListGroupPathSortKey));
				OnPropertyChanged(nameof(ListGroupNameSortKey));
			}
		}
		bool _IsPinned;

		[DefaultValue(null)]
		public DateTime? Created
		{
			get => _Created;
			set
			{
				SetProperty(ref _Created, value);
				OnPropertyChanged(nameof(ListGroupTime));
				OnPropertyChanged(nameof(ListGroupNameSortKey));
			}
		}
		DateTime? _Created;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeCreated() => _Created != null;


		[DefaultValue(null)]
		public DateTime? Modified
		{
			get => _Modified;
			set
			{
				SetProperty(ref _Modified, value);
				OnPropertyChanged(nameof(ListGroupTime));
				OnPropertyChanged(nameof(ListGroupNameSortKey));
			}
		}
		DateTime? _Modified;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeModified() => _Modified != null;

		/// <summary>
		/// Computed property for group name
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public int ListGroupTimeSortKey
		{
			get
			{
				if (IsPinned)
					return 0;
				var today = DateTime.Today;
				var yesterday = today.AddDays(-1);
				var lastWeekStart = today.AddDays(-7);
				// First day of last month
				var lastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
				var d0 = Modified;
				if (d0 == null)
					return int.MaxValue;
				var d = d0.Value;
				// If Created is today											
				if (d.Date == today)
					return 1;
				// If Created is yesterday
				else if (d.Date == yesterday)
					return 2;
				// If Created is within last week
				else if (d.Date >= lastWeekStart && d.Date < yesterday)
					return 3;
				// If Created is within last month
				else if (d.Year == lastMonth.Year && d.Month == lastMonth.Month)
					return 4;
				// If Created is within the current year
				else if (d.Year == today.Year)
					return 10 + d.Month;
				// If none of the above, return the year
				else
					return 100 + d.Year;
			}
		}

		/// <summary>
		/// Computed property for group name
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string ListGroupTime
		{
			get
			{
				if (IsPinned)
					return "Pinned";
				var today = DateTime.Today;
				var yesterday = today.AddDays(-1);
				var lastWeekStart = today.AddDays(-7);
				var lastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
				var d0 = Modified;
				if (d0 == null)
					return "";
				var d = d0.Value;
				if (d.Date == today)
					return "Today";
				else if (d.Date == yesterday)
					return "Yesterday";
				else if (d.Date >= lastWeekStart && d.Date < yesterday)
					return "Last Week";
				else if (d.Year == lastMonth.Year && d.Month == lastMonth.Month)
					return "Last Month";
				else if (d.Year == today.Year)
					return d.ToString("MMMM");
				else
					return d.Year.ToString();
			}
		}

		/// <summary>
		/// Computed property for group name
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string ListGroupPathSortKey
		{
			get
			{
				if (IsPinned)
					return "0";
				return string.IsNullOrEmpty(Path)
					? "1"
					: "2" + Path;
			}
		}


		[XmlIgnore, JsonIgnore]
		public string ListGroupPath
		{
			get
			{
				if (IsPinned)
					return "Pinned";
				return string.IsNullOrEmpty(Path)
					? "Main"
					: Path;
			}
		}

		/// <summary>
		/// Computed property for group name
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string ListGroupNameSortKey
		{
			get
			{
				if (IsPinned)
					return "0";
				var groups = (Name ?? "").Split(new string[] { " - " }, StringSplitOptions.None);
				return groups.Length <= 1
					? "1"
					: "2" + (!Name.StartsWith("®")
						? "4" + groups[0]
						: "5" + groups[0]);
			}
		}


		[XmlIgnore, JsonIgnore]
		public string ListGroupName
		{
			get
			{
				if (IsPinned)
					return "Pinned";
				var groups = (Name ?? "").Split(new string[] { " - " }, StringSplitOptions.None);
				return groups.Length <= 1 ? "Main" : groups[0];
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
