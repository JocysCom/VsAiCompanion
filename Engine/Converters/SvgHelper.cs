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

	}
}
