using System.IO;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Configuration
{
	public interface ISettingsItemFile
	{
		string Name { get; set; }

		[XmlIgnore]
		FileInfo ItemFileInfo { get; set; }

	}
}
