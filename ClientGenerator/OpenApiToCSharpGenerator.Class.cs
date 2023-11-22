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
			//sb.AppendLine("using System;");
			sb.AppendLine("using System.Collections.Generic;");
			//sb.AppendLine();
			sb.AppendLine($"namespace {BaseNamespace}");
			sb.AppendLine("{");

			if (schema.OneOf != null && schema.OneOf.Any())
			{
				// If OneOf is used, consider representing it as an abstract class / interface.
				sb.AppendLine($"    public abstract class {className}");
			}
			else if (schema.Properties.Count == 0 && schema.Extensions.Count > 0)
			{
				// Example of how to handle extensions.
				// You'll have to adjust this logic to fit your specific requirements.
				sb.AppendLine($"    public class {className}");
			}
			else
			{
				// Normal class generation with properties.
				var baseSchema = FindBaseSchema(schema);
				var baseClassName = GetCSharpClassName(baseSchema?.Reference?.Id ?? base_class);
				sb.AppendLine($"    public class {className}{(!string.IsNullOrEmpty(baseClassName) ? $" : {baseClassName}" : "")}");
			}
			sb.AppendLine("    {");

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
					//sb.AppendLine($"        /// <summary>");
					//sb.AppendLine($"        /// Gets or sets the {propertyName}.");
					//sb.AppendLine($"        /// </summary>");
					sb.AppendLine($"        public {propertyType} {propertyName} {{ get; set; }}");
					sb.AppendLine();
				}
			}
			else if (schema.OneOf != null && schema.OneOf.Any())
			{
				// Handle OneOf, possibly create derived classes or use interfaces.
				sb.AppendLine("        // OneOf definitions need to be implemented based on the schemas provided.");
			}
			else if (schema.Extensions.Count > 0)
			{
				// Handle Extensions in some logic that applies to your requirements.
				sb.AppendLine("        // Extensions logic needs to be implemented based on the schemas provided.");
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");
			return sb.ToString();
		}


	}
}
