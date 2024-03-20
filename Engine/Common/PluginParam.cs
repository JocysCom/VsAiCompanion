using System.Text.Json.Serialization;
using System.Windows;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginParam
	{
		public string Type { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public bool IsOptional { get; set; }
		public int Index { get; set; }

		[XmlIgnore, JsonIgnore]
		public object ParamValuePreview { get; set; }

		[XmlIgnore, JsonIgnore]
		public Visibility ParamVisibility { get; set; } = Visibility.Visible;

	}
}
