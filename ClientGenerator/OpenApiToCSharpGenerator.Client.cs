using Microsoft.OpenApi.Models;
using System.Text;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	/// <summary>
	/// Try to generate models and classes like they are described on OpenAI page.
	/// </summary>
	public partial class OpenApiToCSharpGenerator
	{

		public void GenerateClient(OpenApiDocument document, string outputPath)
		{
			// Initialize a string builder to construct the IClient interface
			var sb = new StringBuilder();
			sb.AppendLine($"using {BaseNamespace};");
			sb.AppendLine();
			sb.AppendLine("public interface IClient");
			sb.AppendLine("{");
			// Iterate through each path and method in the OpenAPI document
			foreach (var path in document.Paths)
			{
				foreach (var operation in path.Value.Operations)
				{
					// Generate method signature based on the operation, considering parameters and responses
					// Use GetPrimarySchemaByAlias to determine the actual return type when necessary
					var methodSignature = GenerateMethodSignature(path.Key, operation.Key, operation.Value, GetPrimarySchemaByAlias);
					sb.AppendLine(methodSignature);
				}
			}
			sb.AppendLine("}");
			// Write the interfaceBuilder content to IClient.cs file in outputPath
			string interfaceFilePath = Path.Combine(outputPath, "IClient.cs");
			var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
			WriteHelper.WriteIfDifferent(interfaceFilePath, bytes);
		}


		/// <summary>
		/// This method will need to be implemented to generate each method signature based on OpenAPI Operation
		/// </summary>
		private string GenerateMethodSignature(string path, OperationType operationType, OpenApiOperation operation, Func<OpenApiSchema, OpenApiSchema> getPrimarySchema)
		{
			// Extract the method name from operation ID or generate a new one based on the path and operation type
			var methodName = operation.OperationId ?? $"{operationType}{path.Replace("/", string.Empty)}";
			methodName = GetCSharpTypeName(methodName);

			// Extract return type (check for primary schema if it's an alias)
			string returnType = "void"; // Default return type if none is specified
			if (operation.Responses.TryGetValue("200", out var response) && response.Content.Any())
			{
				var mediaType = response.Content.First().Value;
				var schema = mediaType.Schema;
				var primarySchema = getPrimarySchema(schema);
				// Assume we have a way to convert the schema to a C# type name
				returnType = GetCSharpTypeName(primarySchema);
			}

			// Start building the method signature
			var signatureBuilder = new StringBuilder();
			signatureBuilder.Append($"\t{returnType} {methodName}(");

			// Extract parameters from operation and add them to the method signature
			foreach (var parameter in operation.Parameters)
			{
				string parameterType = GetCSharpTypeName(getPrimarySchema(parameter.Schema));

				if (parameter.In == ParameterLocation.Header)
				{
					// Headers might be optional and have default values, handle them accordingly
					// Assuming there's a helper method to get default value as string representation for C# code
					string defaultValue = parameter.Schema.Default != null ? $" = {GetDefaultValueAsString(parameter.Schema.Default)}" : string.Empty;
					signatureBuilder.Append($"{parameterType} {parameter.Name}{defaultValue}, ");
				}
				else if (parameter.In == ParameterLocation.Query || parameter.In == ParameterLocation.Path)
				{
					// Query and path parameters would be regular method parameters
					signatureBuilder.Append($"{parameterType} {parameter.Name}, ");
				}
			}

			// Check if there is a body parameter and add it, assuming only one body is allowed
			if (operation.RequestBody != null && operation.RequestBody.Content.Any())
			{
				var schema = getPrimarySchema(operation.RequestBody.Content.First().Value.Schema);
				var requestBodyType = GetCSharpTypeName(schema);
				signatureBuilder.Append($"{requestBodyType} body, ");
			}

			// Remove trailing ", " if parameters have been added
			if (operation.Parameters.Count > 0 || operation.RequestBody != null)
			{
				signatureBuilder.Length -= 2;
			}

			signatureBuilder.Append(");");

			return signatureBuilder.ToString();
		}

		private string GetDefaultValueAsString(dynamic defaultValue)
		{
			// Placeholder for logic to convert OpenAPI default value to C# default value string representation
			return "default";
		}
	}
}
