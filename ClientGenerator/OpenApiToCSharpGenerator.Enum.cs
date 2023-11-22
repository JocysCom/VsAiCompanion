using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	public partial class OpenApiToCSharpGenerator
	{

		private string GenerateEnum(OpenApiSchema schema)
		{
			var sb = new StringBuilder();
			var enumName = GetCSharpClassName(schema.Reference.Id);
			sb.AppendLine("using System;");
			sb.AppendLine();
			sb.AppendLine($"namespace {BaseNamespace}");
			sb.AppendLine("{");
			sb.AppendLine($"    public enum {enumName}");
			sb.AppendLine("    {");
			// Assume that the enum values are strings; adjust as needed if enums are integers or other types
			var enumValues = schema.Enum.OfType<OpenApiString>().Select(e => e.Value);
			foreach (var value in enumValues)
			{
				// Replace invalid characters and generate the enum member identifier
				var enumMemberIdentifier = GetValidEnumMemberIdentifier(value);

				// Append the enum value to the StringBuilder
				sb.AppendLine($"        {enumMemberIdentifier},");
			}
			// Remove the trailing comma from the last enum member
			if (sb.ToString().EndsWith(",\n", StringComparison.Ordinal))
				sb.Remove(sb.Length - 2, 1);
			sb.AppendLine("    }");
			sb.AppendLine("}");
			return sb.ToString();
		}

		private string GetValidEnumMemberIdentifier(string enumValue)
		{
			// Basic implementation to convert enum value to a valid C# identifier
			// It replaces spaces with underscores and prefixes with
			// an underscore if it starts with a number
			var identifier = Regex.Replace(enumValue, @"\s+", "_"); // Replace whitespace with underscore
			identifier = Regex.Replace(identifier, @"[^\w_]", ""); // Remove invalid characters
																   // Enum member identifiers cannot start with a digit
			if (char.IsDigit(identifier[0]))
				identifier = "_" + identifier;
			// Enum member identifiers cannot be reserved keywords, prepend with "@" if necessary
			if (ReservedKeywords.Contains(identifier))
				identifier = "@" + identifier;
			return identifier;
		}

	}
}
