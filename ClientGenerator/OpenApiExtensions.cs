using Microsoft.OpenApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{

	/// <summary>
	/// Bridges the Microsoft.OpenApi 3.x object model onto the shapes this generator was written against.
	/// </summary>
	/// <remarks>
	/// Three 3.x changes drive everything here:
	/// <list type="bullet">
	/// <item>A schema no longer carries its own name. Component schemas are named by their key in
	/// <see cref="OpenApiComponents.Schemas"/>, and a <c>$ref</c> surfaces as an <see cref="OpenApiSchemaReference"/>.</item>
	/// <item><see cref="IOpenApiSchema.Type"/> is a <see cref="JsonSchemaType"/> flags enum instead of a string,
	/// and the old <c>Nullable</c> flag is now the <see cref="JsonSchemaType.Null"/> bit.</item>
	/// <item><c>Enum</c> and <c>Default</c> hold <see cref="JsonNode"/> values instead of <c>IOpenApiAny</c>.</item>
	/// </list>
	/// </remarks>
	public static class OpenApiExtensions
	{

		/// <summary>
		/// Name of the component a <c>$ref</c> points at, or null when the schema is declared inline.
		/// </summary>
		public static string? GetReferenceId(this IOpenApiSchema? schema)
			=> (schema as OpenApiSchemaReference)?.Reference?.Id;

		/// <summary>
		/// True when the schema is a <c>$ref</c> rather than an inline definition.
		/// Replaces the 1.x <c>schema.Reference != null</c> test.
		/// </summary>
		public static bool IsReference(this IOpenApiSchema? schema)
			=> schema is OpenApiSchemaReference;

		/// <summary>
		/// True when the schema declares exactly the given JSON Schema type, ignoring nullability.
		/// Replaces the 1.x string comparison such as <c>schema.Type == "string"</c>.
		/// </summary>
		public static bool IsType(this IOpenApiSchema? schema, JsonSchemaType type)
			=> schema?.Type != null && (schema.Type.Value & ~JsonSchemaType.Null) == type;

		/// <summary>
		/// True when the schema permits null. 3.x removed the standalone <c>Nullable</c> property in
		/// favour of the <see cref="JsonSchemaType.Null"/> bit.
		/// </summary>
		public static bool IsNullable(this IOpenApiSchema? schema)
			=> schema?.Type != null && schema.Type.Value.HasFlag(JsonSchemaType.Null);

		/// <summary>
		/// The declared type with the nullability bit stripped, so two schemas that differ only in
		/// nullability still compare as the same underlying type.
		/// </summary>
		public static JsonSchemaType? GetBaseType(this IOpenApiSchema? schema)
			=> schema?.Type == null ? null : schema.Type.Value & ~JsonSchemaType.Null;

		/// <summary>
		/// String members of an enum schema. 3.x stores enum values as <see cref="JsonNode"/>,
		/// so non-string members (numbers, nulls) are skipped rather than throwing.
		/// </summary>
		public static IEnumerable<string> GetEnumStringValues(this IOpenApiSchema schema)
		{
			if (schema.Enum == null)
				yield break;
			foreach (var node in schema.Enum)
			{
				if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
					yield return value.GetValue<string>();
			}
		}

		/// <summary>
		/// True when the schema carries an enumeration of allowed values.
		/// </summary>
		public static bool HasEnum(this IOpenApiSchema? schema)
			=> schema?.Enum != null && schema.Enum.Count > 0;

		private static readonly IDictionary<string, IOpenApiSchema> NoProperties
			= new Dictionary<string, IOpenApiSchema>();

		/// <summary>
		/// Declared properties, never null. 1.x always materialised an empty dictionary,
		/// whereas 3.x leaves the collection null when the keyword is absent.
		/// </summary>
		public static IDictionary<string, IOpenApiSchema> GetProperties(this IOpenApiSchema? schema)
			=> schema?.Properties ?? NoProperties;

		/// <summary>
		/// Vendor extensions, which 3.x exposes on the concrete types rather than on
		/// <see cref="IOpenApiSchema"/> itself.
		/// </summary>
		public static IDictionary<string, IOpenApiExtension>? GetExtensions(this IOpenApiSchema? schema)
			=> (schema as IOpenApiExtensible)?.Extensions;

		/// <summary>
		/// Number of vendor extensions, or zero when the schema exposes none.
		/// </summary>
		public static int GetExtensionCount(this IOpenApiSchema? schema)
			=> schema.GetExtensions()?.Count ?? 0;

	}
}
