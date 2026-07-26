using Microsoft.OpenApi;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

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

			// Resolve a `$ref` before applying the alias mapping, which is keyed by component schema.
			Func<IOpenApiSchema, IOpenApiSchema> getPrimarySchema =
				s => GetPrimarySchemaByAlias(ResolveSchema(s));

			var paths = document.Paths ?? new OpenApiPaths();

			// Check if any operations return or accept List<> types
			bool needsGenericCollections = false;

			foreach (var path in paths)
			{
				foreach (var operation in GetOperations(path.Value))
				{
					// Check response types
					if (GetResponses(operation.Value).Any(r => GetContent(r.Value.Content).Any(c =>
					{
						var schema = c.Value.Schema;
						return schema != null && (schema.IsType(JsonSchemaType.Array) ||
							   (schema.IsReference() && GetCSharpTypeName(schema).Contains("List<")));
					})))
					{
						needsGenericCollections = true;
					}

					// Check parameter types
					if (GetParameters(operation.Value).Any(p =>
					{
						var schema = p.Schema;
						return schema != null && (schema.IsType(JsonSchemaType.Array) ||
							   (schema.IsReference() && GetCSharpTypeName(schema).Contains("List<")));
					}))
					{
						needsGenericCollections = true;
					}

					// Check request body
					if (GetContent(operation.Value.RequestBody?.Content).Any(c =>
					{
						var schema = c.Value.Schema;
						return schema != null && (schema.IsType(JsonSchemaType.Array) ||
							   (schema.IsReference() && GetCSharpTypeName(schema).Contains("List<")));
					}))
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
			foreach (var path in paths)
			{
				foreach (var operation in GetOperations(path.Value))
				{
					// Generate method signature based on the operation, considering parameters and responses
					// Use GetPrimarySchemaByAlias to determine the actual return type when necessary
					var methodSignature = GenerateMethodSignature(path.Key, operation.Key, operation.Value, getPrimarySchema);
					sb.AppendLine(methodSignature);
				}
			}
			sb.AppendLine("}");
			// Write the interfaceBuilder content to IClient.cs file in outputPath
			string interfaceFilePath = Path.Combine(outputPath, "IClient.cs");
			var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
			WriteHelper.WriteIfDifferent(interfaceFilePath, bytes);
		}

		#region Null-safe accessors

		// Microsoft.OpenApi 3.x leaves absent collections null instead of materialising empty ones.

		private static IEnumerable<KeyValuePair<HttpMethod, OpenApiOperation>> GetOperations(IOpenApiPathItem pathItem)
			=> (IEnumerable<KeyValuePair<HttpMethod, OpenApiOperation>>?)pathItem?.Operations
				?? Array.Empty<KeyValuePair<HttpMethod, OpenApiOperation>>();

		private static IEnumerable<KeyValuePair<string, IOpenApiResponse>> GetResponses(OpenApiOperation operation)
			=> (IEnumerable<KeyValuePair<string, IOpenApiResponse>>?)operation?.Responses
				?? Array.Empty<KeyValuePair<string, IOpenApiResponse>>();

		private static IEnumerable<KeyValuePair<string, IOpenApiMediaType>> GetContent(IDictionary<string, IOpenApiMediaType>? content)
			=> content ?? (IEnumerable<KeyValuePair<string, IOpenApiMediaType>>)Array.Empty<KeyValuePair<string, IOpenApiMediaType>>();

		private static IList<IOpenApiParameter> GetParameters(OpenApiOperation operation)
			=> operation?.Parameters ?? (IList<IOpenApiParameter>)Array.Empty<IOpenApiParameter>();

		#endregion

		/// <summary>
		/// Generates method signature based on OpenAPI Operation
		/// </summary>
		private string GenerateMethodSignature(string path, HttpMethod operationType, OpenApiOperation operation, Func<IOpenApiSchema, IOpenApiSchema> getPrimarySchema)
		{
			// Extract the method name from operation ID or generate a new one based on the path and operation type
			// 3.x keys operations by HttpMethod, whose ToString() is upper case ("GET"); title case it so the
			// generated identifier still reads as `get_pets` rather than `g_e_t_pets`.
			var methodPrefix = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(operationType.Method.ToLowerInvariant());
			var methodName = operation.OperationId ?? $"{methodPrefix}{path.Replace("/", string.Empty)}";
			methodName = GetCSharpTypeName(methodName);

			// Extract return type (check for primary schema if it's an alias)
			string returnType = "void"; // Default return type if none is specified
			var responses = operation.Responses;
			if (responses != null && responses.TryGetValue("200", out var response) && GetContent(response.Content).Any())
			{
				var mediaType = GetContent(response.Content).First().Value;
				var schema = mediaType.Schema;
				if (schema != null)
				{
					var primarySchema = getPrimarySchema(schema);

					if (schema.IsReference())
					{
						// For referenced types, use the resolved component name to get the class name
						var className = GetCSharpClassName(GetSchemaName(primarySchema));

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
			}

			// Start building the method signature
			var signatureBuilder = new StringBuilder();
			signatureBuilder.Append($"\t{returnType} {methodName}(");

			// Extract parameters from operation and add them to the method signature
			var parameters = GetParameters(operation);
			foreach (var parameter in parameters)
			{
				// 3.x allows a parameter to omit its schema; fall back to `object` rather than throwing.
				var parameterSchema = parameter.Schema;
				string parameterType = "object";
				if (parameterSchema is OpenApiSchemaReference)
				{
					var className = GetCSharpClassName(GetSchemaName(ResolveSchema(parameterSchema)));

					// Explicitly check if the class name is a reserved keyword
					if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
					{
						parameterType = "@" + className;
					}
					else
					{
						parameterType = className;
					}

					if (parameterSchema.IsNullable())
						parameterType += "?";
				}
				else if (parameterSchema != null)
				{
					parameterType = GetCSharpTypeName(getPrimarySchema(parameterSchema));
				}

				string parameterName = GetCSharpTypeName(parameter.Name ?? string.Empty);

				if (parameter.In == ParameterLocation.Header)
				{
					string defaultValue = parameterSchema?.Default != null ? $" = {GetDefaultValueAsString(parameterSchema.Default)}" : string.Empty;
					signatureBuilder.Append($"{parameterType} {parameterName}{defaultValue}, ");
				}
				else if (parameter.In == ParameterLocation.Query || parameter.In == ParameterLocation.Path)
				{
					signatureBuilder.Append($"{parameterType} {parameterName}, ");
				}
			}

			// Check if there is a body parameter and add it
			var requestBodyContent = GetContent(operation.RequestBody?.Content);
			if (operation.RequestBody != null && requestBodyContent.Any())
			{
				var schema = requestBodyContent.First().Value.Schema;
				string requestBodyType = "object";

				if (schema is OpenApiSchemaReference)
				{
					var className = GetCSharpClassName(GetSchemaName(getPrimarySchema(schema)));

					// Explicitly check if the class name is a reserved keyword
					if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
					{
						requestBodyType = "@" + className;
					}
					else
					{
						requestBodyType = className;
					}

					if (schema.IsNullable())
						requestBodyType += "?";
				}
				else if (schema != null)
				{
					requestBodyType = GetCSharpTypeName(getPrimarySchema(schema));
				}

				signatureBuilder.Append($"{requestBodyType} body, ");
			}

			// Remove trailing ", " if parameters have been added
			if (parameters.Count > 0 || operation.RequestBody != null)
			{
				signatureBuilder.Length -= 2;
			}

			signatureBuilder.Append(");");

			return signatureBuilder.ToString();
		}

		private string GetDefaultValueAsString(JsonNode defaultValue)
		{
			// Placeholder for logic to convert OpenAPI default value to C# default value string representation
			return "default";
		}
	}
}
