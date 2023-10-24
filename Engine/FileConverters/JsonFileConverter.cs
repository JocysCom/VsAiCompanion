using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DocumentFormat.OpenXml.Wordprocessing;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	internal class JsonConverter
	{

		public static void WriteAsXml(string path, List<chat_completion_request> o)
		{
			JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlFile(o, path);
		}

		public static List<chat_completion_request> ReadFromXml(string path)
		{
			return JocysCom.ClassLibrary.Runtime.Serializer.DeserializeFromXmlFile<List<chat_completion_request>>(path);
		}

		public static void WriteAsJson(string path, List<chat_completion_request> o)
		{
			var contents = Client.Serialize(o);
			File.WriteAllText(path, contents);
		}

		public static List<chat_completion_request> ReadFromJson(string path)
		{
			var json = File.ReadAllText(path);
			return Client.Deserialize<List<chat_completion_request>>(json);
		}

		#region Rich Text Format (*.rtf)

		static void AddRtfLine(System.Windows.Forms.RichTextBox rtf, string text = null, bool isBold = false)
		{
			if (!string.IsNullOrEmpty(text))
			{
				int startPos = rtf.Text.Length;
				rtf.AppendText(text);
				rtf.Select(startPos, text.Length);
				if (isBold)
					rtf.SelectionFont = new System.Drawing.Font(rtf.Font, FontStyle.Bold);
			}
			rtf.AppendText("\n");
		}

		public static void WriteAsRtf(string path, List<chat_completion_request> o)
		{
			var rtf = new System.Windows.Forms.RichTextBox();
			foreach (var request in o)
			{
				foreach (var message in request.messages)
				{
					if (message.role == message_role.user)
					{
						AddRtfLine(rtf, message.content, true);
						AddRtfLine(rtf);
					}
					if (message.role == message_role.assistant)
					{
						AddRtfLine(rtf, message.content);
						AddRtfLine(rtf);
					}
				}
			}
			rtf.SaveFile(path, System.Windows.Forms.RichTextBoxStreamType.RichText);
		}

		#endregion

		#region Word Document (*.docx)

		static void AddDocxParagraph(Body body, string text = null, bool isBold = false)
		{
			Paragraph para = body.AppendChild(new Paragraph());
			Run run = para.AppendChild(new Run());
			if (!string.IsNullOrEmpty(text))
			{
				run.AppendChild(new Text(text));
				if (isBold)
				{
					run.RunProperties = new RunProperties();
					run.RunProperties.AppendChild(new Bold());
				}
			}
		}

		public static void WriteAsDocx(string path, List<chat_completion_request> o)
		{
			using (var wordDocument = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
			{
				var mainPart = wordDocument.AddMainDocumentPart();
				mainPart.Document = new Document(new Body());
				foreach (var request in o)
				{
					foreach (var message in request.messages)
					{
						if (message.role == message_role.user)
						{
							AddDocxParagraph(mainPart.Document.Body, message.content, true);
							AddDocxParagraph(mainPart.Document.Body);
						}
						if (message.role == message_role.assistant)
						{
							AddDocxParagraph(mainPart.Document.Body, message.content);
							AddDocxParagraph(mainPart.Document.Body);
						}
					}
				}
				mainPart.Document.Save();
				wordDocument.Dispose();
			}
		}

		#endregion

	}
}
