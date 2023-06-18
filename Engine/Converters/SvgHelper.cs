using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.IO;
using System.Text;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Converters
{
	public static class SvgHelper
	{
		public static DrawingImage LoadSvgFromFile(string path)
		{
			var text = File.ReadAllText(path);
			return LoadSvgFromString(text);
		}

		public static DrawingImage LoadSvgFromBytes(byte[] svgContent)
		{
			var stream = new MemoryStream(svgContent);
			var reader = new StreamReader(stream, Encoding.Default, true);
			var text = reader.ReadToEnd();
			reader.Dispose();
			return LoadSvgFromString(text);
		}

		public static DrawingImage LoadSvgFromString(string svgContent)
		{
			if (string.IsNullOrEmpty(svgContent))
				return null;
			var reader = new StringReader(svgContent);
			var settings = new WpfDrawingSettings
			{
				IncludeRuntime = true,
				TextAsGeometry = false,
				OptimizePath = true,
			};
			var converter = new FileSvgReader(settings);
			var drawingGroup = converter.Read(reader);
			reader.Dispose();
			return new DrawingImage(drawingGroup);
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

	}
}
