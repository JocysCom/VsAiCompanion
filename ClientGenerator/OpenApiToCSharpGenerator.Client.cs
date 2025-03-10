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

			// Check if any operations return or accept List<> types
			bool needsGenericCollections = false;

			foreach (var path in document.Paths)
			{
				foreach (var operation in path.Value.Operations)
				{
					// Check response types
					if (operation.Value.Responses.Any(r => r.Value.Content.Any(c =>
					{
						var schema = c.Value.Schema;
						return schema != null && (schema.Type == "array" ||
							   (schema.Reference != null && GetCSharpTypeName(schema).Contains("List<")));
					})))
					{
						needsGenericCollections = true;
					}

					// Check parameter types
					if (operation.Value.Parameters.Any(p =>
					{
						var schema = p.Schema;
						return schema != null && (schema.Type == "array" ||
							   (schema.Reference != null && GetCSharpTypeName(schema).Contains("List<")));
					}))
					{
						needsGenericCollections = true;
					}

					// Check request body
					if (operation.Value.RequestBody?.Content.Any(c =>
					{
						var schema = c.Value.Schema;
						return schema != null && (schema.Type == "array" ||
							   (schema.Reference != null && GetCSharpTypeName(schema).Contains("List<")));
					}) == true)
					{
						needsGenericCollections = true;
					}
				}
			}

			sb.AppendLine($"using {BaseNamespace};");
			if (needsGenericCollections)
			{
				sb.AppendLine($"using System.Collections.Generic;");
			}
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
		/// Generates method signature based on OpenAPI Operation
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

				if (primarySchema.Reference != null)
				{
					// For referenced types, use the reference ID to get the class name
					var refId = primarySchema.Reference.Id;
					var className = GetCSharpClassName(refId);

					// Explicitly check if the class name is a reserved keyword
					if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
					{
						returnType = "@" + className;
					}
					else
					{
						returnType = className;
					}
				}
				else
				{
					// For non-reference types, use the regular type mapping
					returnType = GetCSharpTypeName(primarySchema);
				}
			}

			// Start building the method signature
			var signatureBuilder = new StringBuilder();
			signatureBuilder.Append($"\t{returnType} {methodName}(");

			// Extract parameters from operation and add them to the method signature
			foreach (var parameter in operation.Parameters)
			{
				string parameterType;
				if (parameter.Schema.Reference != null)
				{
					var refId = parameter.Schema.Reference.Id;
					var className = GetCSharpClassName(refId);

					// Explicitly check if the class name is a reserved keyword
					if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
					{
						parameterType = "@" + className;
					}
					else
					{
						parameterType = className;
					}

					if (parameter.Schema.Nullable)
						parameterType += "?";
				}
				else
				{
					parameterType = GetCSharpTypeName(getPrimarySchema(parameter.Schema));
				}

				string parameterName = GetCSharpTypeName(parameter.Name);

				if (parameter.In == ParameterLocation.Header)
				{
					string defaultValue = parameter.Schema.Default != null ? $" = {GetDefaultValueAsString(parameter.Schema.Default)}" : string.Empty;
					signatureBuilder.Append($"{parameterType} {parameterName}{defaultValue}, ");
				}
				else if (parameter.In == ParameterLocation.Query || parameter.In == ParameterLocation.Path)
				{
					signatureBuilder.Append($"{parameterType} {parameterName}, ");
				}
			}

			// Check if there is a body parameter and add it
			if (operation.RequestBody != null && operation.RequestBody.Content.Any())
			{
				var schema = operation.RequestBody.Content.First().Value.Schema;
				string requestBodyType;

				if (schema.Reference != null)
				{
					var primarySchema = getPrimarySchema(schema);
					var refId = primarySchema.Reference.Id;
					var className = GetCSharpClassName(refId);

					// Explicitly check if the class name is a reserved keyword
					if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
					{
						requestBodyType = "@" + className;
					}
					else
					{
						requestBodyType = className;
					}

					if (schema.Nullable)
						requestBodyType += "?";
				}
				else
				{
					requestBodyType = GetCSharpTypeName(getPrimarySchema(schema));
				}

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
