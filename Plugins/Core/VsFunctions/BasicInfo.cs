using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Basic AI audio or image info.
	/// </summary>
	public class BasicInfo
	{
		/// <summary>Image file name.</summary>
		public string Name { get; set; }

		/// <summary>Image full file name.</summary>
		public string FullName { get; set; }

		/// <summary>AI Prompt used to generate this image.</summary>
		public string Prompt { get; set; }

		/// <summary>Type of the object.</summary>
		public ContextType Type { get; set; }

		/// <summary>Suffix to add to the file.</summary>
		[XmlIgnore, JsonIgnore]
		public string FileSuffix => "";

	}
}
