namespace JocysCom.VS.AiCompanion.ClientGenerator
{
	internal class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Please provide the path to openapi.yaml and the output directory as arguments.");
				return;
			}
			string yamlFilePath = args[0];
			string outputDirectory = args[1];
			bool nullable = false;
			if (args.Length > 2)
				bool.TryParse(args[2], out nullable);

			if (!File.Exists(yamlFilePath))
			{
				Console.WriteLine("The specified YAML file does not exist.");
				return;
			}

			if (!Directory.Exists(outputDirectory))
			{
				Console.WriteLine("The specified output directory does not exist.");
				return;
			}

			var generator = new OpenApiToCSharpGenerator(nullable);
			Console.WriteLine($"Generating client models to {outputDirectory}");
			var yamlContents = File.ReadAllText(yamlFilePath);
			var document = YamlHelper.ConvertToDocument(yamlContents);
			if (document == null)
				return;
			generator.GenerateModels(document, outputDirectory);
			generator.GenerateClient(document, outputDirectory);
		}
	}
}
