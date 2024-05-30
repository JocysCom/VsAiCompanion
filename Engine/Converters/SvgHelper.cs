using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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

		/// <summary>
		/// Use a cache dictionary to make sure the same SVG content isn't loaded multiple times.
		/// This will help the app use about 200MB less memory for SVG images.
		/// </summary>
		private static ConcurrentDictionary<string, DrawingImage> _defaultValuesCache = new ConcurrentDictionary<string, DrawingImage>();

		private static string GetHashString(string s)
		{
			using (var algorithm = System.Security.Cryptography.SHA256.Create())
			{
				var bytes = System.Text.Encoding.UTF8.GetBytes(s);
				var hash = algorithm.ComputeHash(bytes);
				var hashString = string.Join("", hash.Select(x => x.ToString("X2")));
				return hashString;
			}
		}

		public static DrawingImage LoadSvgFromString(string svgContent)
		{
			var hash = GetHashString(svgContent);
			return _defaultValuesCache.GetOrAdd(hash, t => _LoadSvgFromString(svgContent));
		}

		public static DrawingImage _LoadSvgFromString(string svgContent)
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
