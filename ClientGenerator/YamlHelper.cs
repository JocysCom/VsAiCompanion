using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.YamlReader;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{
	public class YamlHelper
	{
		/// <summary>
		/// Microsoft.OpenApi 3.x only parses JSON out of the box, so the YAML reader is registered once here.
		/// </summary>
		private static readonly OpenApiReaderSettings ReaderSettings = CreateReaderSettings();

		private static OpenApiReaderSettings CreateReaderSettings()
		{
			var settings = new OpenApiReaderSettings();
			settings.AddYamlReader();
			return settings;
		}

		public static OpenApiDocument? ConvertToDocument(string yamlContent)
		{
			var result = OpenApiModelFactory.Parse(yamlContent, OpenApiConstants.Yaml, ReaderSettings);

			if (result.Diagnostic?.Errors.Count > 0)
			{
				// Handle error case here, consider logging the errors too.
				Console.WriteLine("Errors parsing OpenAPI document:");
				foreach (var error in result.Diagnostic.Errors)
					Console.WriteLine(error.Message);
				return default;
			}
			return result.Document;
		}

	}
}
