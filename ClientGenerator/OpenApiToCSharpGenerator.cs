using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.OpenApi.Readers;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Any;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	/// <summary>
	/// Try to generate models and classes like they are described on OpenAI page.
	/// </summary>
	public partial class OpenApiToCSharpGenerator
	{

		private const string BaseNamespace = "JocysCom.VS.AiCompanion.Clients.OpenAI.Models";

		public string base_class = "base_item";

		public Dictionary<string, string> overideClassNames = new Dictionary<string, string>() {
			{"embedding", "embedding_item" }
		};

		private List<OpenApiSchema> knownSchemas = new List<OpenApiSchema>();
		private List<OpenApiSchema> FoundClasses = new List<OpenApiSchema>();
		private List<OpenApiSchema> FoundEnums = new List<OpenApiSchema>();

		public void GenerateModelsFromOpenApiYaml(string yamlContent, string outputDirectory)
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
					{
						Console.WriteLine(error.Message);
					}
					return;
				}
				var allShemas = openApiDocument.Components.Schemas.Select(x => x.Value).ToList();
				FoundClasses = allShemas
					.Where(x => x.Enum == null || !x.Enum.Any())
					.ToList();
				FoundEnums = allShemas
					.Where(x => x.Enum != null && x.Enum.Any())
					.ToList();
				PopulateAliasMapping();
				// Exclude all aliases.
				FoundClasses = FoundClasses.Except(schemaAliasMapping.Keys).ToList();
				PopulateBaseProperties();
				// Iterate through enums
				FilesBefore = Directory.GetFiles(outputDirectory + "\\Enums", "*.cs").ToList();
				foreach (var schema in FoundEnums)
				{
					var id = schema.Reference.Id;
					var csharpClassContent = GenerateCSharpEnum(schema);
					string filePath = Path.Combine(outputDirectory + "\\Enums", GetCSharpClassName(id) + ".cs");
					SaveToFile(filePath, csharpClassContent);
				}
				CleanupFiles(outputDirectory + "\\Enums");
				// Iterate through classes, noting aliases and generating classes
				FilesBefore = Directory.GetFiles(outputDirectory + "\\Models", "*.cs").ToList();
				foreach (var schema in FoundClasses)
				{
					var id = schema.Reference.Id;
					var csharpClassContent = GenerateCSharpClass(schema);
					string filePath = Path.Combine(outputDirectory + "\\Models", GetCSharpClassName(id) + ".cs");
					SaveToFile(filePath, csharpClassContent);
				}
				CleanupFiles(outputDirectory + "\\Models");
			}
		}

		public List<string> FilesBefore = new List<string>();
		public List<string> FilesAfter = new List<string>();

		public void CleanupFiles(string folder)
		{
			FilesAfter = Directory.GetFiles(folder, "*.cs").ToList();
			var filesToDelete = FilesBefore.Except(FilesAfter);
			foreach (var file in filesToDelete)
				File.Delete(file);
		}

		#region Populate Base Properties

		/// <summary>
		/// Contains namse of all properties inherited from base classes.
		/// </summary>
		private Dictionary<OpenApiSchema, HashSet<string>> baseProperties = new Dictionary<OpenApiSchema, HashSet<string>>();

		private void PopulateBaseProperties()
		{
			baseProperties.Clear(); // Clear any existing entries in the dictionary

			foreach (var schema in FoundClasses)
			{
				// Get all properties from base class hierarchy
				var allBaseProperties = GetAllBaseProperties(schema);

				// Populate the baseProperties dictionary with the result
				baseProperties[schema] = allBaseProperties;
			}
		}

		/// <summary>
		/// Get all base properties of a schema, including inherited ones from base schemas
		/// </summary>
		private HashSet<string> GetAllBaseProperties(OpenApiSchema schema)
		{
			var properties = new HashSet<string>();
			var currentSchema = schema;
			while (currentSchema != null)
			{
				OpenApiSchema? baseSchema = FindBaseSchema(currentSchema);
				if (baseSchema == null)
				{
					break; // No more base schema found, stop the loop
				}

				// Add base schema properties if the base schema is valid
				if (baseSchema.Properties != null)
				{
					foreach (var property in baseSchema.Properties)
					{
						properties.Add(property.Key);
					}
				}

				// Move up the inheritance chain
				currentSchema = baseSchema;
			}
			return properties;
		}

		#endregion

		#region Populate Schema Alias Mapping

		///<summary>
		///Maintains a mapping of schema aliases to their respective primary schema.
		///</summary>
		private Dictionary<OpenApiSchema, OpenApiSchema> schemaAliasMapping = new Dictionary<OpenApiSchema, OpenApiSchema>();

		///<summary>
		///Attempt to map schema aliases to their respective primary schema, considering each schema only once.
		///Repeatedly calls MapSchemaAlias until no new primary types are found.
		///</summary>
		public void PopulateAliasMapping()
		{
			bool foundNewPrimary;
			do
			{
				foundNewPrimary = MapSchemaAlias();
			}
			while (foundNewPrimary);
		}

		public OpenApiSchema GetPrimarySchemaByAlias(OpenApiSchema schema)
		{
			return schemaAliasMapping.ContainsKey(schema)
				? schemaAliasMapping[schema]
				: schema;
		}

		///<summary>
		///Maps schemas to their aliases based on whether they have identical properties, considering each schema only once.
		///Prioritizes schemas with shorter type names and higher sort order as primary schemas. Returns true if new primary
		///types were found.
		///</summary>
		private bool MapSchemaAlias()
		{
			bool foundNewPrimary = false;
			for (int a = 0; a < FoundClasses.Count; a++)
			{
				for (int b = a + 1; b < FoundClasses.Count; b++)
				{
					OpenApiSchema schemaA = FoundClasses[a];
					OpenApiSchema schemaB = FoundClasses[b];
					schemaA = GetPrimarySchemaByAlias(schemaA);
					schemaB = GetPrimarySchemaByAlias(schemaB);
					if (schemaA != schemaB && AreSchemasAliases(schemaA, schemaB))
					{
						OpenApiSchema primarySchema = ChoosePrimarySchema(schemaA, schemaB);
						OpenApiSchema aliasSchema = (primarySchema == schemaA) ? schemaB : schemaA;
						if (schemaAliasMapping.TryAdd(aliasSchema, primarySchema))
							foundNewPrimary = true;
						// Consolidate all the aliases of the non-primary to point to the detected primary schema.
						foreach (var pair in schemaAliasMapping.Where(p => p.Value == aliasSchema).ToList())
							schemaAliasMapping[pair.Key] = primarySchema;
					}
				}
			}

			return foundNewPrimary;
		}

		///<summary>
		///Chooses the primary schema based on the shorter type name and sort order.
		///</summary>
		private static OpenApiSchema ChoosePrimarySchema(OpenApiSchema schemaA, OpenApiSchema schemaB)
		{
			// Retrieve type name or reference ID as applicable
			var typeNameA = schemaA.Type ?? schemaA.Reference?.Id ?? "";
			var typeNameB = schemaB.Type ?? schemaB.Reference?.Id ?? "";

			// If either schema does not have a type or a reference, it can't be compared
			if (typeNameA == null || typeNameB == null)
			{
				throw new InvalidOperationException("Cannot determine primary schema: one or both schemas lack type information.");
			}

			int compareLength = typeNameA.Length.CompareTo(typeNameB.Length);
			if (compareLength == 0)
			{
				// If the type names or reference IDs are of the same length, use sort order to decide
				return string.Compare(typeNameA, typeNameB, StringComparison.Ordinal) < 0 ? schemaA : schemaB;
			}

			// Choose the schema with the shorter type name or reference ID as the primary schema
			return compareLength < 0 ? schemaA : schemaB;
		}

		///<summary>
		///Determines whether two schemas can be considered aliases based on their properties.
		///This comparison includes only property names and types. Consider enhancing the comparison mechanism 
		///with additional schema constraints for a more sophisticated comparison.
		///</summary>
		///<remarks>
		///The properties are compared by both names and types but may need to extend the comparison 
		///with additional schema constraints for a more sophisticated comparison.
		///</remarks>
		private static bool AreSchemasAliases(OpenApiSchema schemaA, OpenApiSchema schemaB)
		{
			if (schemaA.Properties.Count != schemaB.Properties.Count)
				return false;
			foreach (var propA in schemaA.Properties)
			{
				if (!schemaB.Properties.TryGetValue(propA.Key, out var propB) || propA.Value.Type != propB.Type)
					return false;
			}
			return true;
		}

		#endregion

		#region Generate C# Enum

		private string GenerateCSharpEnum(OpenApiSchema schema)
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

		#endregion

		#region Generate C# Class

		private string GenerateCSharpClass(OpenApiSchema schema)
		{
			var sb = new StringBuilder();
			var className = GetCSharpClassName(schema.Reference.Id);
			//sb.AppendLine("using System;");
			//sb.AppendLine("using System.Collections.Generic;");
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


		private static readonly string[] numericTypes =
		{
			"int",  "double",  "decimal",
			"long", "short",   "sbyte",
			"byte", "ulong",   "ushort",
			"uint", "float",
		};

		/// <summary>
		/// Map OpenAPI schema types to C# types.
		/// </summary>
		/// <param name="schema">The schema to get the C# type for.</param>
		/// <param name="enableNullable">Determines whether nullable suffix is allowed for value types.</param>
		/// <returns>String representation of the corresponding C# type.</returns>
		private string GetCSharpTypeName(OpenApiSchema schema, bool enableNullable = false)
		{
			var csType = "object";
			// Handle simple types
			if (schema.Type == "string")
				csType = "string";
			if (schema.Type == "integer")
				csType = schema.Format == "int64" ? "long" : "int";
			if (schema.Type == "boolean")
				csType = "bool";
			if (schema.Type == "number")
				csType = schema.Format == "float" ? "float" : "double";
			// Handle complex types
			if (schema.Reference != null)
			{
				var primarySchema = GetPrimarySchemaByAlias(schema);
				csType = GetCSharpClassName(primarySchema.Reference.Id);
			}
			// Handle arrays
			if (schema.Type == "array" && schema.Items != null)
				csType = $"List<{GetCSharpTypeName(schema.Items, enableNullable)}>";
			var isValueType = numericTypes.Contains(csType) ||
				(schema.Enum != null && schema.Enum.Any());
			// Handle nullable types for reference types or if nullable is enabled for value types
			if (schema.Nullable && (enableNullable || isValueType))
				csType += "?";
			return csType;
		}

		/// <summary>
		/// Determines whether the OpenAPI schema represents a reference type in C#.
		/// </summary>
		/// <param name="schema">The schema to check.</param>
		/// <returns>True if the schema corresponds to a reference type, false otherwise.</returns>
		private bool IsReferenceType(OpenApiSchema schema)
		{
			// Add other reference types as necessary
			return schema.Type == "string" || schema.Type == "object" || schema.Reference != null ||
				   (schema.Type == "array" && schema.Items != null);
		}

		#endregion


		private static readonly HashSet<string> ReservedKeywords = new HashSet<string>
		{
			// Keywords
			"abstract", "as", "base", "bool", "break", "byte",
			"case", "catch", "char", "checked", "class", "const",
			"continue", "decimal", "default", "delegate", "do", "double",
			"else", "enum", "event", "explicit", "extern", "false",
			"finally", "fixed", "float", "for", "foreach", "goto",
			"if", "implicit", "in", "int", "interface", "internal",
			"is", "lock", "long", "namespace", "new", "null",
			"object", "operator", "out", "override", "params", "private",
			"protected", "public", "readonly", "ref", "return", "sbyte",
			"sealed", "short", "sizeof", "stackalloc", "static", "string",
			"struct", "switch", "this", "throw", "true", "try",
			"typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
			"using", "virtual", "void", "volatile", "while",

			// Contextual keywords
			"add", "alias", "ascending", "async", "await", "by",
			"descending", "dynamic", "equals", "from", "get", "global",
			"group", "into", "join", "let", "nameof", "on",
			"orderby", "partial", "remove", "select", "set", "value",
			"var", "when", "where", "yield"
		};

		private string GetCSharpClassName(string input)
		{
			input = GetCSharpTypeName(input);
			return !string.IsNullOrEmpty(input) && overideClassNames.ContainsKey(input)
				? overideClassNames[input]
				: input;
		}


		/// <returns>C# class name with prefix `@` for reserved words.</returns>
		private string GetCSharpTypeName(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;
			var pattern = @"(?<!^)([A-Z])"; // Negative lookbehind to avoid matching the start of the string
			var result = Regex.Replace(input, pattern, m => "_" + m.Groups[1].Value).ToLower();
			input = result.Trim('_');
			var isCSharpKeyword = ReservedKeywords.Contains(input);
			return isCSharpKeyword
				? "@" + input
				: input;
		}

		public string GetSchemaName(OpenApiSchema? schema)
		{
			if (schema == null)
				return string.Empty;
			var s =
				knownSchemas.FirstOrDefault(kv => kv.Equals(schema)) ??
				FoundClasses.FirstOrDefault(kv => kv.Equals(schema));
			return s?.Reference?.Id ?? "";
		}

		/// <summary>
		/// Return the best candidate for the base class. It for 
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="candidates"></param>
		private OpenApiSchema? FindBaseSchema(OpenApiSchema schema)
		{
			var currentSchemaPropertyNames = new HashSet<string>(schema.Properties.Keys);
			var candidateSchemas = knownSchemas
				.Concat(FoundClasses)
				.Except(new[] { schema })
				// Must have properties.
				.Where(x => x.Properties.Count > 0)
				.ToList();

			OpenApiSchema? baseSchema = null;
			int maxMatchingProperties = -1;

			foreach (var candidate in candidateSchemas)
			{
				var candidatePropertyNames = new HashSet<string>(candidate.Properties.Keys);

				// Ensure that the candidate has strictly fewer properties
				if (candidatePropertyNames.Count < currentSchemaPropertyNames.Count)
				{
					var matchingPropertiesCount = candidatePropertyNames.Count(currentSchemaPropertyNames.Contains);

					// Update base schema if this candidate has more matching properties than the current best match,
					// but still has strictly fewer properties overall
					if (matchingPropertiesCount > maxMatchingProperties)
					{
						maxMatchingProperties = matchingPropertiesCount;
						baseSchema = candidate;
					}
				}
			}
			return baseSchema;
		}

		/// <summary>
		/// Returns true if both objects contain same properties.
		/// </summary>
		private static bool IsSame(OpenApiSchema a, OpenApiSchema b)
		{
			var count = a.Properties.Count();
			if (count != b.Properties.Count())
				return false;
			var isSame = count == CountMatchingProperties(a, b);
			return isSame;
		}

		/// <summary>
		/// Count mathing properties.
		/// </summary>
		private static int CountMatchingProperties(OpenApiSchema a, OpenApiSchema b)
		{
			var sameCount = a.Properties.Count(p => b.Properties.ContainsKey(p.Key) && b.Properties[p.Key].Type == p.Value.Type);
			return sameCount;
		}

		/// <summary>
		/// Write generated C# content to the file.
		/// </summary>
		/// <param name="path">Path to the C# file.</param>
		/// <param name="contents">C# file contents to write.</param>
		private void SaveToFile(string path, string contents)
		{
			var bytes = Encoding.UTF8.GetBytes(contents);
			WriteHelper.WriteIfDifferent(path, bytes);
			Console.WriteLine($"Saved: {path}");
		}


	}
}
