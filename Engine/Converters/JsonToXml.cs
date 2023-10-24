using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine.Converters
{
	internal class JsonToXmlFileConverter
	{
		public static void ConvertTo(string sourceFileName, string targetFileName)
		{
			var json = File.ReadAllText(sourceFileName);
			var items = Client.Deserialize<chat_completion_request[]>(json);
			var xml = JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlString(items);
			File.WriteAllText(targetFileName, xml);
		}

		public static void ConvertFrom(string sourceFileName, string targetFileName)
		{
			var json = File.ReadAllText(sourceFileName);
			var items = Client.Deserialize<chat_completion_request[]>(json);
			var xml = JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlString(items);
			File.WriteAllText(targetFileName, xml);
		}


	}
}
