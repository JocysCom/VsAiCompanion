using Microsoft.OpenApi.Models;
using System.Text;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	public partial class OpenApiToCSharpGenerator
	{

		private string GenerateClass(OpenApiSchema schema)
		{
			var sb = new StringBuilder();
			var className = GetCSharpClassName(schema.Reference.Id);

			// Check if the class name is a C# reserved keyword and add @ prefix if needed
			if (ReservedKeywords.Contains(className) && !className.StartsWith("@"))
			{
				className = "@" + className;
			}
			// Add @ prefix to class names that contain only lowercase ASCII characters [a-z]+
			else if (className.Length > 0 && className.All(c => c >= 'a' && c <= 'z'))
			{
				className = "@" + className;
			}

			// Check if any property uses List<> before adding the using statement
			bool needsGenericCollections = schema.Properties.Values.Any(p =>
				p.Type == "array" || (p.Reference != null && GetCSharpTypeName(p).Contains("List<")));

			if (needsGenericCollections)
			{
				sb.AppendLine("using System.Collections.Generic;");
			}

			sb.AppendLine($"namespace {BaseNamespace}");
			sb.AppendLine("{");

			if (schema.OneOf != null && schema.OneOf.Any())
			{
				// If OneOf is used, consider representing it as an abstract class / interface.
				sb.AppendLine($"\tpublic abstract class {className}");
			}
			else if (schema.Properties.Count == 0 && schema.Extensions.Count > 0)
			{
				// Example of how to handle extensions.
				// You'll have to adjust this logic to fit your specific requirements.
				sb.AppendLine($"\tpublic class {className}");
			}
			else
			{
				// Normal class generation with properties.
				var baseSchema = FindBaseSchema(schema);
				var baseClassName = GetCSharpClassName(baseSchema?.Reference?.Id ?? base_class);

				// Also check if base class name is a reserved keyword
				if (ReservedKeywords.Contains(baseClassName) && !baseClassName.StartsWith("@"))
				{
					baseClassName = "@" + baseClassName;
				}

				sb.AppendLine($"\tpublic class {className}{(!string.IsNullOrEmpty(baseClassName) ? $" : {baseClassName}" : "")}");
			}
			sb.AppendLine("\t{");

			if (schema.Properties.Count > 0)
			{
				var basePropertyNameSet = baseProperties[schema];
				foreach (var property in schema.Properties)
				{
					if (basePropertyNameSet.Contains(property.Key))
						continue; // Skip inherited property
					var propertyName = GetCSharpTypeName(property.Key);
					var propertySchema = property.Value;

					// Resolve the property schema to its primary schema if it's an alias
					if (schemaAliasMapping.TryGetValue(propertySchema, out var primarySchema))
					{
						propertySchema = primarySchema;
					}
					var propertyType = GetCSharpTypeName(propertySchema);
					sb.AppendLine($"\t\tpublic {propertyType} {propertyName} {{ get; set; }}");
					sb.AppendLine();
				}
			}
			else if (schema.OneOf != null && schema.OneOf.Any())
			{
				// Handle OneOf, possibly create derived classes or use interfaces.
				sb.AppendLine("\t\t// OneOf definitions need to be implemented based on the schemas provided.");
			}
			else if (schema.Extensions.Count > 0)
			{
				// Handle Extensions in some logic that applies to your requirements.
				sb.AppendLine("\t\t// Extensions logic needs to be implemented based on the schemas provided.");
			}

			sb.AppendLine("\t}");
			sb.AppendLine("}");
			return sb.ToString();
		}


	}
}
