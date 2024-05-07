using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class VisemeItem
	{

		public VisemeItem() { }

		public VisemeItem(int offset, int visemeId)
		{
			Offset = offset;
			VisemeId = visemeId;
		}

		[XmlAttribute]
		public int Offset { get; set; }

		[XmlAttribute]
		public int VisemeId { get; set; }

	}
}
