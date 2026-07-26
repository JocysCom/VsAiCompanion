using Microsoft.OpenApi;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	/// <summary>
	/// Try to generate models and classes like they are described on OpenAI page.
	/// </summary>
	public partial class OpenApiToCSharpGenerator
	{
		public bool EnableNullable { get; set; }

		public OpenApiToCSharpGenerator(bool enableNullable)
		{
			EnableNullable = enableNullable;
		}

		private const string BaseNamespace = "JocysCom.VS.AiCompanion.Clients.OpenAI.Models";

		public string base_class = "base_item";

		public Dictionary<string, string> overideClassNames = new Dictionary<string, string>() {
			{"embedding", "embedding_item" },
			{"open_a_i_file", "file" },
		};

		private List<IOpenApiSchema> knownSchemas = new List<IOpenApiSchema>();
		private List<IOpenApiSchema> FoundClasses = new List<IOpenApiSchema>();
		private List<IOpenApiSchema> FoundEnums = new List<IOpenApiSchema>();

		/// <summary>
		/// Component schemas by name, used to resolve a `$ref` back to its definition.
		/// </summary>
		private Dictionary<string, IOpenApiSchema> componentsByName = new Dictionary<string, IOpenApiSchema>();

		/// <summary>
		/// Reverse of <see cref="componentsByName"/>. Microsoft.OpenApi 3.x dropped the per-schema
		/// `Reference` object, so a schema's name is only recoverable from the components dictionary key.
		/// </summary>
		private Dictionary<IOpenApiSchema, string> schemaNames = new Dictionary<IOpenApiSchema, string>();

		public void GenerateModels(OpenApiDocument document, string outputDirectory)
		{
			IndexComponentSchemas(document);
			var allShemas = componentsByName.Values.ToList();
			FoundClasses = allShemas
				.Where(x => !x.HasEnum())
				.ToList();
			FoundEnums = allShemas
				.Where(x => x.HasEnum())
				.ToList();
			PopulateAliasMapping();
			// Exclude all aliases.
			FoundClasses = FoundClasses.Except(schemaAliasMapping.Keys).ToList();
			PopulateBaseProperties();
			var enumsPath = Path.Combine(outputDirectory, "Enums");
			var modelsPath = Path.Combine(outputDirectory, "Models");
			if (!Directory.Exists(enumsPath))
				Directory.CreateDirectory(enumsPath);
			if (!Directory.Exists(modelsPath))
				Directory.CreateDirectory(modelsPath);
			// Iterate through enums
			FilesBefore = Directory.GetFiles(enumsPath, "*.cs").ToList();
			foreach (var schema in FoundEnums)
			{
				var id = GetSchemaName(schema);
				var csharpClassContent = GenerateEnum(schema);
				string filePath = Path.Combine(enumsPath, GetCSharpClassName(id) + ".cs");
				WriteHelper.SaveToFile(filePath, csharpClassContent, true);
			}
			CleanupFiles(enumsPath);
			// Iterate through classes, noting aliases and generating classes
			FilesBefore = Directory.GetFiles(modelsPath, "*.cs").ToList();
			foreach (var schema in FoundClasses)
			{
				var id = GetSchemaName(schema);
				var csharpClassContent = GenerateClass(schema);
				string filePath = Path.Combine(modelsPath, GetCSharpClassName(id) + ".cs");
				WriteHelper.SaveToFile(filePath, csharpClassContent, true);
			}
			CleanupFiles(modelsPath);
		}

		#region Schema Naming and Reference Resolution

		/// <summary>
		/// Record the name of every component schema so it can be recovered later.
		/// </summary>
		private void IndexComponentSchemas(OpenApiDocument document)
		{
			componentsByName.Clear();
			schemaNames.Clear();
			var schemas = document.Components?.Schemas;
			if (schemas == null)
				return;
			foreach (var pair in schemas)
			{
				componentsByName[pair.Key] = pair.Value;
				// Two names can point at the same schema instance; the first one wins.
				if (!schemaNames.ContainsKey(pair.Value))
					schemaNames.Add(pair.Value, pair.Key);
			}
		}

		/// <summary>
		/// Follow a `$ref` to the component schema it names. Inline schemas are returned unchanged.
		/// </summary>
		private IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
		{
			var refId = schema.GetReferenceId();
			return refId != null && componentsByName.TryGetValue(refId, out var target)
				? target
				: schema;
		}

		#endregion

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
		private Dictionary<IOpenApiSchema, HashSet<string>> baseProperties = new Dictionary<IOpenApiSchema, HashSet<string>>();

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
		private HashSet<string> GetAllBaseProperties(IOpenApiSchema schema)
		{
			var properties = new HashSet<string>();
			var currentSchema = schema;
			while (currentSchema != null)
			{
				IOpenApiSchema? baseSchema = FindBaseSchema(currentSchema);
				if (baseSchema == null)
				{
					break; // No more base schema found, stop the loop
				}

				// Add base schema properties if the base schema is valid
				foreach (var property in baseSchema.GetProperties())
				{
					properties.Add(property.Key);
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
		private Dictionary<IOpenApiSchema, IOpenApiSchema> schemaAliasMapping = new Dictionary<IOpenApiSchema, IOpenApiSchema>();

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

		public IOpenApiSchema GetPrimarySchemaByAlias(IOpenApiSchema schema)
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
					IOpenApiSchema schemaA = FoundClasses[a];
					IOpenApiSchema schemaB = FoundClasses[b];
					schemaA = GetPrimarySchemaByAlias(schemaA);
					schemaB = GetPrimarySchemaByAlias(schemaB);
					if (schemaA != schemaB && AreSchemasAliases(schemaA, schemaB))
					{
						IOpenApiSchema primarySchema = ChoosePrimarySchema(schemaA, schemaB);
						IOpenApiSchema aliasSchema = (primarySchema == schemaA) ? schemaB : schemaA;
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
		private IOpenApiSchema ChoosePrimarySchema(IOpenApiSchema schemaA, IOpenApiSchema schemaB)
		{
			// Retrieve type name or component name as applicable
			var typeNameA = schemaA.GetBaseType()?.ToString() ?? GetSchemaName(schemaA);
			var typeNameB = schemaB.GetBaseType()?.ToString() ?? GetSchemaName(schemaB);

			// If either schema does not have a type or a name, it can't be compared
			if (string.IsNullOrEmpty(typeNameA) || string.IsNullOrEmpty(typeNameB))
			{
				throw new InvalidOperationException("Cannot determine primary schema: one or both schemas lack type information.");
			}

			int compareLength = typeNameA.Length.CompareTo(typeNameB.Length);
			if (compareLength == 0)
			{
				// If the type names or names are of the same length, use sort order to decide
				return string.Compare(typeNameA, typeNameB, StringComparison.Ordinal) < 0 ? schemaA : schemaB;
			}

			// Choose the schema with the shorter type name or name as the primary schema
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
		private static bool AreSchemasAliases(IOpenApiSchema schemaA, IOpenApiSchema schemaB)
		{
			var propertiesA = schemaA.GetProperties();
			var propertiesB = schemaB.GetProperties();
			if (propertiesA.Count != propertiesB.Count)
				return false;
			foreach (var propA in propertiesA)
			{
				// Compare the base type only. 1.x kept nullability in a separate `Nullable` flag, so
				// properties differing solely in nullability still matched; 3.x folds it into `Type`.
				if (!propertiesB.TryGetValue(propA.Key, out var propB) || propA.Value.GetBaseType() != propB.GetBaseType())
					return false;
			}
			return true;
		}

		#endregion

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
		/// <returns>String representation of the corresponding C# type.</returns>
		private string GetCSharpTypeName(IOpenApiSchema schema)
		{
			var csType = "object";
			// Handle simple types
			if (schema.IsType(JsonSchemaType.String))
				csType = "string";
			else if (schema.IsType(JsonSchemaType.Integer))
				csType = schema.Format == "int64" ? "long" : "int";
			else if (schema.IsType(JsonSchemaType.Boolean))
				csType = "bool";
			else if (schema.IsType(JsonSchemaType.Number))
				csType = schema.Format == "float" ? "float" : "double";
			else if (schema.IsType(JsonSchemaType.Array) && schema.Items != null)
				csType = $"List<{GetCSharpTypeName(schema.Items)}>";

			// Handle complex types
			// Check if it is a reference to another complex type such as classes or enums
			if (schema.IsReference())
			{
				// Resolve the `$ref` to its definition first, because the alias mapping is keyed
				// by component schema, not by the reference that points at it.
				var primarySchema = GetPrimarySchemaByAlias(ResolveSchema(schema));
				// Enums and classes are both emitted as a single named C# type.
				csType = GetCSharpClassName(GetSchemaName(primarySchema));
			}
			// Determine if the type is a numeric value type
			var isValueType = numericTypes.Contains(csType);
			// Handle nullable types for value types
			if (schema.IsNullable() && (EnableNullable || isValueType))
				csType += "?";

			return csType;
		}

		/// <returns>C# class name with prefix `@` for reserved words.</returns>
		private string GetCSharpTypeName(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;
			// First, replace any non-alphanumeric characters (except underscores) with underscores
			// This handles dashes, spaces, and other invalid characters
			input = Regex.Replace(input, @"[^\w]", "_");
			var pattern = @"(?<!^)([A-Z])"; // Negative lookbehind to avoid matching the start of the string
			var result = Regex.Replace(input, pattern, m => "_" + m.Groups[1].Value).ToLower();
			input = result.Trim('_');
			var isCSharpKeyword = ReservedKeywords.Contains(input);
			return isCSharpKeyword
				? "@" + input
				: input;
		}

		/// <summary>
		/// Determines whether the OpenAPI schema represents a reference type in C#.
		/// </summary>
		/// <param name="schema">The schema to check.</param>
		/// <returns>True if the schema corresponds to a reference type, false otherwise.</returns>
		private bool IsReferenceType(IOpenApiSchema schema)
		{
			// Add other reference types as necessary
			return schema.IsType(JsonSchemaType.String) || schema.IsType(JsonSchemaType.Object) || schema.IsReference() ||
				   (schema.IsType(JsonSchemaType.Array) && schema.Items != null);
		}

		private static readonly HashSet<string> ReservedKeywords = new HashSet<string>
		{
			// Keywords
			"abstract", "as", "base", "bool", "break", "byte",
			"case", "catch", "char", "checked", "class", "const",
			"continue", "decimal", "default", "delegate", "do", "double",
			"else", "enum", "event", "explicit", "extern", "false",
			"file", "finally", "fixed", "float", "for", "foreach", "goto",
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

		/// <summary>
		/// Component name of a schema, resolving a `$ref` to the schema it points at.
		/// </summary>
		public string GetSchemaName(IOpenApiSchema? schema)
		{
			if (schema == null)
				return string.Empty;
			if (schemaNames.TryGetValue(schema, out var name))
				return name;
			// A `$ref` carries the target name even when the target itself was not indexed.
			return schema.GetReferenceId() ?? string.Empty;
		}

		/// <summary>
		/// Return the best candidate for the base class. It for
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="candidates"></param>
		private IOpenApiSchema? FindBaseSchema(IOpenApiSchema schema)
		{
			var currentSchemaPropertyNames = new HashSet<string>(schema.GetProperties().Keys);
			var candidateSchemas = knownSchemas
				.Concat(FoundClasses)
				.Except(new[] { schema })
				// Must have properties.
				.Where(x => x.GetProperties().Count > 0)
				.ToList();

			IOpenApiSchema? baseSchema = null;
			int maxMatchingProperties = -1;

			foreach (var candidate in candidateSchemas)
			{
				var candidatePropertyNames = new HashSet<string>(candidate.GetProperties().Keys);

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
		private static bool IsSame(IOpenApiSchema a, IOpenApiSchema b)
		{
			var count = a.GetProperties().Count;
			if (count != b.GetProperties().Count)
				return false;
			var isSame = count == CountMatchingProperties(a, b);
			return isSame;
		}

		/// <summary>
		/// Count mathing properties.
		/// </summary>
		private static int CountMatchingProperties(IOpenApiSchema a, IOpenApiSchema b)
		{
			var propertiesB = b.GetProperties();
			// Base type only, for the same reason as AreSchemasAliases.
			var sameCount = a.GetProperties().Count(p => propertiesB.ContainsKey(p.Key) && propertiesB[p.Key].GetBaseType() == p.Value.GetBaseType());
			return sameCount;
		}

	}
}
