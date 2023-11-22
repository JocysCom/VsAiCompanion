using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Text;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{
	public class YamlHelper
	{
		public static OpenApiDocument? ConvertToDocument(string yamlContent)
		{

			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(yamlContent)))
			{
				var reader = new OpenApiStreamReader();
				var openApiDocument = reader.Read(stream, out var diagnostic);

				if (diagnostic.Errors.Count > 0)
				{
					// Handle error case here, consider logging the errors too.
					Console.WriteLine("Errors parsing OpenAPI document:");
					foreach (var error in diagnostic.Errors)
						Console.WriteLine(error.Message);
					return default;
				}
				return openApiDocument;
			}
		}

	}
}
