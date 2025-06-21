#if !NETFRAMEWORK
#nullable disable
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Xml
{

	public static partial class XmlDocHelper
	{

		#region Methods

		/// <summary>Retrieves the XML &lt;summary&gt; text for a member, applying the specified formatting.</summary>
		/// <remarks>If no member summary is present and the member is a property, falls back to the property's type summary.</remarks>
		public static string GetSummaryText(MemberInfo mi, FormatText format = FormatText.None)
		{
			var member = GetMemberDoc(mi);
			// Get <summary> of the property first.
			var s = member?.summary ?? "";
			// If not set then...
			if (!string.IsNullOrEmpty(s))
				return GetFormattedText(s, format);
			if (mi.MemberType == MemberTypes.Property)
			{
				//	Get class  summary.
				var pi = mi as PropertyInfo;
				s = GetSummary(pi.PropertyType);
			}
			s = GetFormattedText(s, format);
			return s;
		}

		/// <summary>Retrieves the XML &lt;example&gt; text for a member, applying the specified formatting.</summary>
		/// <remarks>If no example is present and the member is a property, falls back to the property's type summary.</remarks>
		public static string GetExampleText(MemberInfo mi, FormatText format = FormatText.None)
		{
			var member = GetMemberDoc(mi);
			var s = member?.example ?? "";
			// If not set then...
			if (!string.IsNullOrEmpty(s))
				return GetFormattedText(s, format);
			if (mi.MemberType == MemberTypes.Property)
			{
				//	Get class  summary.
				var pi = mi as PropertyInfo;
				s = GetSummary(pi.PropertyType);
			}
			s = GetFormattedText(s, format);
			return s;
		}


		/// <summary>Retrieves the XML &lt;param&gt; documentation for a method parameter, applying the specified formatting.</summary>
		/// <remarks>If no param tag is found, falls back to the parameter type's summary.</remarks>
		public static string GetParamText(MethodInfo mi, ParameterInfo pi, FormatText format = FormatText.None)
		{
			var member = GetMemberDoc(mi);
			var s = member?.param.Where(x => x.name == pi.Name).Select(x => x.value).FirstOrDefault() ?? "";
			if (!string.IsNullOrEmpty(s))
				return GetFormattedText(s, format);
			// Get return class summary.
			s = GetSummary(pi.ParameterType);
			s = GetFormattedText(s, format);
			return s;
		}

		/// <summary>Retrieves the XML &lt;returns&gt; documentation for a method, applying the specified formatting.</summary>
		/// <remarks>If no returns tag is found, falls back to the return type's summary.</remarks>
		public static string GetReturnText(MethodInfo mi, FormatText format = FormatText.None)
		{
			var member = GetMemberDoc(mi);
			var s = member?.returns ?? "";
			if (!string.IsNullOrEmpty(s))
				return GetFormattedText(s, format);
			// Get return class summary.
			s = GetSummary(mi.ReturnType);
			s = GetFormattedText(s, format);
			return s;
		}

		/// <summary>Retrieves the XML &lt;summary&gt; documentation for a type, applying the specified formatting.</summary>
		public static string GetSummary(Type type, FormatText format = FormatText.None)
		{
			var ti = type.GetTypeInfo();
			var typeMember = GetMemberDoc(ti);
			var s = typeMember?.summary ?? "";
			s = GetFormattedText(s, format);
			return s;
		}

		/// <summary>Applies FormatText options to a documentation string.</summary>
		/// <remarks>Options include indentation removal, whitespace collapsing, and trimming.</remarks>
		public static string GetFormattedText(string s, FormatText format)
		{
			if (format.HasFlag(FormatText.RemoveIdent))
				s = RemoveIdent(s);
			if (format.HasFlag(FormatText.ReduceSpaces))
				s = ReduceSpaces(s);
			if (format.HasFlag(FormatText.TrimSpaces))
				s = TrimSpaces(s);
			return s;
		}

		/// <summary>Compiled regex matching sequences of whitespace characters (spaces, tabs, newlines, no-break spaces).</summary>
		public static readonly Regex RxMultiSpace = new Regex("[ \r\n\t\u00A0]+", RegexOptions.Compiled);

		/// <summary>Removes common leading indentation from a multi-line string while preserving the first line.</summary>
		public static string RemoveIdent(string s)
		{
			s = s.Trim('\n', '\r', ' ', '\t').Replace("\r\n", "\n");
			var lines = s.Split('\n');
			var checkLines = lines
				// Ignore first trimmed line.
				.Where((x, i) => i > 0 && !string.IsNullOrWhiteSpace(x)).ToArray();
			if (checkLines.Length == 0)
				return s;
			var minIndent = checkLines.Min(x => x.Length - x.TrimStart(' ', '\t').Length);
			for (var i = 0; i < lines.Length; i++)
			{
				if (lines[i].Length > minIndent)
					// Don't trim first line.
					lines[i] = lines[i].Substring(i == 0 ? 0 : minIndent);
				else if (string.IsNullOrWhiteSpace(lines[i]))
					lines[i] = "";
			}
			return string.Join(Environment.NewLine, lines);
		}

		/// <summary>Collapses runs of whitespace characters into a single space and trims leading/trailing whitespace.</summary>
		public static string ReduceSpaces(string s)
		{
			if (string.IsNullOrEmpty(s))
				return s;
			return TrimSpaces(RxMultiSpace.Replace(s, " "));
		}

		/// <summary>Trims whitespace characters from both ends of a string.</summary>
		public static string TrimSpaces(string s)
		{
			if (string.IsNullOrEmpty(s))
				return s;
			return s.Trim(' ', '\r', '\n', '\t', '\u00A0');
		}

		/// <summary>Concatenates InnerText of provided XmlNode array; returns null if nodes is null.</summary>
		public static string ConvertXmlNodesToText(params XmlNode[] nodes)
		{
			if (nodes == null)
				return null;
			var result = string.Empty;
			foreach (XmlNode node in nodes)
				result += node.InnerText;
			return result;
		}

		/// <summary>Concatenates OuterXml of provided XmlNode array; returns empty string if nodes is null.</summary>
		public static string ConvertXmlNodesToXml(params XmlNode[] nodes)
		{
			if (nodes == null)
				return string.Empty;
			var result = string.Empty;
			foreach (XmlNode node in nodes)
				result += node.OuterXml;
			return result;
		}

		#endregion

		/// <summary>Retrieve the XML comments for a type or a member of a type.</summary>
		/// <remarks>For methods without a summary, attempts to find documentation on implemented interface methods.</remarks>
		public static XmlDocMember GetMemberDoc(MemberInfo mi)
		{
			var memberDoc = _GetMemberDoc(mi);
			// If summary found or not MethodInfo type then return.
			if (!string.IsNullOrEmpty(memberDoc?.summary) || !(mi is MethodInfo methodInfo))
				return memberDoc;
			foreach (var intf in methodInfo.DeclaringType.GetInterfaces())
			{
				// Generate the method signature to look for in the XML documentation.
				var map = methodInfo.DeclaringType.GetInterfaceMap(intf);
				for (int i = 0; i < map.TargetMethods.Length; i++)
				{
					if (map.TargetMethods[i] != methodInfo)
						continue;
					// Generate the name according to the XML documentation convention.
					var interfaceMethod = map.InterfaceMethods[i];
					var iMemberDoc = _GetMemberDoc(interfaceMethod);
					if (iMemberDoc != null)
						return iMemberDoc;
				}
			}
			return null;
		}

		/// <summary>Retrieve the XML comments for a type or a member of a type.</summary>
		/// <remarks>Generates documentation element names based on member type, including support for generics and operators.</remarks>
		private static XmlDocMember _GetMemberDoc(MemberInfo mi)
		{
			var declType = (mi is Type) ? ((Type)mi) : mi.DeclaringType;
			var xmlDoc = GetXmlDoc(declType.Assembly);
			if (xmlDoc is null)
				return null;
			var generics = declType.GetGenericArguments();
			if (generics.Length > 0)
				declType = declType.GetGenericTypeDefinition();
			// Plus signs separate nested types from their declaring types.
			// XML documentation use dots. Replace plus signs to dots.
			var typeName = declType.FullName.Replace("+", ".");
			// Based on the member type, get the correct name.
			var name = "";
			switch (mi.MemberType)
			{
				case MemberTypes.NestedType:
				case MemberTypes.TypeInfo:
					name += "T:" + typeName;
					break;
				case MemberTypes.Constructor:
					name += "M:" + typeName;
					name += ".#ctor" + CreateParamsDescription(((ConstructorInfo)mi).GetParameters());
					break;
				case MemberTypes.Method:
					name += "M:" + typeName + ".";
					name += mi.Name + CreateParamsDescription(((MethodInfo)mi).GetParameters());
					if (mi.Name == "op_Implicit" || mi.Name == "op_Explicit")
						name += "~{" + ((MethodInfo)mi).ReturnType.FullName + "}";
					break;
				case MemberTypes.Property:
					name += "P:" + typeName + ".";
					name += mi.Name + CreateParamsDescription(((PropertyInfo)mi).GetIndexParameters());
					break;
				case MemberTypes.Field:
					name += "F:" + typeName + "." + mi.Name;
					break;
				case MemberTypes.Event:
					name += "E:" + typeName + "." + mi.Name;
					break;
				default:
					return null;
			}
			return xmlDoc.members.FirstOrDefault(x => x.name == name);
		}

		#region Parameters

		/// <summary>Generates a parameter string used when searching XML comment files.</summary>
		/// <param name="parameters">List of parameters to a member.</param>
		/// <returns>A parameter string used when searching XML comment files.</returns>
		/// <remarks>Generates comma-separated parameter type list enclosed in parentheses following XML documentation naming conventions.</remarks>
		private static string CreateParamsDescription(ParameterInfo[] parameters)
		{
			var paramDesc = new StringBuilder();
			// Start the list.
			if (parameters.Any())
				paramDesc.Append("(");
			for (var i = 0; i < parameters.Length; i++)
			{
				if (i > 0)
					paramDesc.Append(",");
				var paramType = parameters[i].ParameterType;

				// Check if the type is nullable and format accordingly.
				string paramName = FormatParamTypeName(paramType);

				// Append the fixed up parameter name
				paramDesc.Append(paramName);
			}
			// End the list.
			if (parameters.Any())
				paramDesc.Append(")");
			// Return the parameter list description
			return paramDesc.ToString();
		}

		/// <summary>Format the parameter type name.</summary>
		/// <remarks>Represents nullable types as System.Nullable{FullTypeName} to match XML documentation conventions.</remarks>
		private static string FormatParamTypeName(Type paramType)
		{
			if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				// For nullable types, use a custom format.
				var innerType = paramType.GetGenericArguments()[0];
				return $"System.Nullable{{{innerType.FullName}}}";
			}
			else
			{
				// Default handling for other types.
				return paramType.FullName;
			}
		}


		#endregion

		#region Assembly XML Document Files.

		/// <summary>XML Document cache.</summary>
		private static readonly ConcurrentDictionary<Assembly, XmlDoc> XmlDocCache = new ConcurrentDictionary<Assembly, XmlDoc>();

		/// <summary>XML Document cache.</summary>
		private static readonly ConcurrentDictionary<Assembly, XmlDocument> XmlDocumentCache = new ConcurrentDictionary<Assembly, XmlDocument>();

		/// <summary>Get XML Doc.</summary>
		/// <remarks>Retrieves and caches the XmlDoc model for an assembly; set cache=false to bypass the cache.</remarks>
		public static XmlDoc GetXmlDoc(Assembly assembly, bool cache = true)
		{
			if (assembly is null)
				return null;
			if (!cache)
				return _GetXmlDoc(assembly);
			// If enumeration then use value as a key, otherwise use type string.
			return XmlDocCache.GetOrAdd(assembly, x => _GetXmlDoc(x));
		}

		/// <summary>Loads XML documentation for an assembly and deserializes it into XmlDoc.</summary>
		/// <remarks>Bypasses cache; used internally to fetch and parse XML documentation.</remarks>
		private static XmlDoc _GetXmlDoc(Assembly assembly)
		{
			var xml = GetXmlDocument(assembly);
			if (xml is null)
				return null;
			//var validator = new Runtime.XmlValidator();
			//validator.IsValid<XmlDoc>(xml.OuterXml, true);
			//var exceptions = validator.Exceptions;
			var xmlDoc = (XmlDoc)DeserializeFromXmlString(xml.OuterXml, typeof(XmlDoc));
			return xmlDoc;
		}

		/// <summary>Get XML Document.</summary>
		/// <remarks>Retrieves and caches the XmlDocument for an assembly; set cache=false to bypass the cache.</remarks>
		public static XmlDocument GetXmlDocument(Assembly a, bool cache = true)
		{
			if (a is null)
				return null;
			if (!cache)
				return _GetXmlDocument(a);
			// If enumeration then use value as a key, otherwise use type string.
			return XmlDocumentCache.GetOrAdd(a, x => _GetXmlDocument(x));
		}
		private static XmlDocument _GetXmlDocument(Assembly assembly)
		{
			var path = GetXmlDocumentationPath(assembly);
			var xml = new XmlDocument();
			if (!(path is null))
			{
				xml.Load(path);
				return xml;
			}
			var resourceName = assembly.GetName().Name + ".xml";
			var resourceFullName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith(resourceName));
			if (string.IsNullOrEmpty(resourceFullName))
				return null;
			var stream = assembly.GetManifestResourceStream(resourceFullName);
			using (var reader = new StreamReader(stream))
			{
				var xmlString = reader.ReadToEnd();
				xml.LoadXml(xmlString);
				return xml;
			}
		}
		/// <summary>Gets XML document file path by assembly.</summary>
		public static string GetXmlDocumentationPath(Assembly assembly)
		{
			var locations = new string[]
			{
				// Location of the assembly.
				assembly.Location,
				// Framework runtime directory.
				RuntimeEnvironment.GetRuntimeDirectory() + Path.GetFileName(assembly.Location)
			};
			// Checks locations.
			foreach (var location in locations)
			{
				var xmlPath = Path.ChangeExtension(location, ".xml");
				// If XML file found then...
				if (File.Exists(xmlPath))
					return xmlPath;
			}
			return null;
		}
		#endregion

		#region XML: De-serialize

		static object XmlSerializersLock = new object();
		static Dictionary<Type, XmlSerializer> XmlSerializers { get; set; } = new Dictionary<Type, XmlSerializer>();
		/// <summary>
		/// De-serialize object from XML string. XML string must not contain Byte Order Mark (BOM).
		/// </summary>
		/// <param name="xml">XML string representing object.</param>
		/// <param name="type">Type of object.</param>
		/// <returns>Object.</returns>
		public static object DeserializeFromXmlString(string xml, Type type)
		{
			if (string.IsNullOrEmpty(xml))
				return null;
			// Note: If you are getting de-serialization error in XML document(1,1) then there is a chance that
			// you are trying to de-serialize string which contains Byte Order Mark (BOM) which must not be there.
			// Probably you used "var xml = System.Text.Encoding.GetString(bytes)" directly on file content.
			// You should use "StreamReader" on file content, because this method will strip BOM properly
			// when converting bytes to string.
			// Settings used to protect from
			// SUPPRESS: CWE-611: Improper Restriction of XML External Entity Reference('XXE')
			// https://cwe.mitre.org/data/definitions/611.html
			var settings = new XmlReaderSettings();
			settings.DtdProcessing = DtdProcessing.Ignore;
			settings.XmlResolver = null;
			object o = null;
			lock (XmlSerializersLock)
			{
				if (!XmlSerializers.ContainsKey(type))
				{
					var extraTypes = new Type[] { typeof(string) };
					XmlSerializers.Add(type, new XmlSerializer(type, extraTypes));
				}
			}
			var serializer = XmlSerializers[type];
			//serializer.UnknownElement += (s, e) =>
			//	System.Diagnostics.Debug.WriteLine($"Unknown element: {e.Element.Name} in {e.Element.OuterXml}");
			//serializer.UnknownAttribute += (s, e) =>
			//	System.Diagnostics.Debug.WriteLine($"Unknown attribute: {e.Attr.Name} in {e.Attr.OuterXml}");
			//serializer.UnknownNode += (s, e) =>
			//	System.Diagnostics.Debug.WriteLine($"Unknown node: {e.Name}, Text: {e.Text}");
			//serializer.UnreferencedObject += (s, e) =>
			//	System.Diagnostics.Debug.WriteLine($"Unreferenced object: {e.UnreferencedId} in {e.UnreferencedObject.GetType().FullName}");
			// Stream 'sr' will be disposed by the reader.
			using (var sr = new StringReader(xml))
			{
				using (var reader = XmlReader.Create(sr, settings))
					lock (serializer) { o = serializer.Deserialize(reader); }
				return o;
			}
		}
		#endregion
	}

	#region Helper Classes

	///<summary>
	/// Specifies formatting options for XML documentation text.
	/// </summary>
	[Flags]
	public enum FormatText
	{
		None = 0,
		RemoveIdent = 1,
		ReduceSpaces = 2,
		TrimSpaces = 4,
		RemoveIdentAndTrimSpaces = RemoveIdent | TrimSpaces,
		ReduceAndTrimSpaces = ReduceSpaces | TrimSpaces,
	}

	///<summary>
	/// Represents the type and name of an XML documentation entity.
	/// </summary>
	public class XmlDocName
	{
		/// <summary>
		/// The type of the XML documentation entity (e.g., "T" for type, "M" for method).
		/// </summary>
		public string type;
		/// <summary>
		/// The fully qualified name of the XML documentation entity.
		/// </summary>
		public string name;
	}

	///<summary>
	/// Represents the assembly information in an XML documentation file.
	/// </summary>
	[Serializable]
	[XmlType(AnonymousType = true)]
	public class XmlDocAssembly
	{
		/// <summary>
		/// The name of the assembly.
		/// </summary>
		public string name { get; set; }
	}

	///<summary>
	/// Represents the root element of an XML documentation file.
	/// </summary>
	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("doc")]
	public class XmlDoc
	{
		/// <summary>
		/// Information about the assembly to which the documentation belongs.
		/// </summary>
		public XmlDocAssembly assembly;
		/// <summary>
		/// A list of documented members.
		/// </summary>
		[XmlArray("members")]
		[XmlArrayItem("member")]
		public List<XmlDocMember> members;
	}

	///<summary>
	/// Represents a parameter or return value documentation in an XML documentation file.
	/// </summary>
	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("param")]
	public partial class XmlDocParam
	{
		/// <summary>
		/// The name of the parameter.
		/// </summary>
		[XmlAttribute]
		public string name;
		/// <summary>
		/// The description of the parameter or return value.
		/// </summary>
		[XmlText]
		public string value { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("member")]
	public class XmlDocMember
	{
		/// <summary>Name attribute.</summary>
		[XmlAttribute]
		public string name { get; set; }

		/// <summary>Summary Nodes element</summary>
		/// <remarks>Use `XmlAnyElement` to allow self closing tags with `XmlNode[]`.</remarks>
		[XmlAnyElement("summary")]
		public XmlNode[] summaryNodes { get; set; }

		/// <summary>Summary text</summary>
		[XmlIgnore]
		public string summary => XmlDocHelper.ConvertXmlNodesToText(summaryNodes);

		/// <summary>Param element</summary>
		[XmlElement("param")]
		public List<XmlDocParam> param { get; set; }

		/// <summary>Returns nodes</summary>
		/// <remarks>Use `XmlAnyElement` to allow self closing tags with `XmlNode[]`.</remarks>
		[XmlAnyElement("returns")]
		public XmlNode[] returnsNodes { get; set; }

		/// <summary>Returns text</summary>
		[XmlIgnore]
		public string returns => XmlDocHelper.ConvertXmlNodesToText(returnsNodes);

		/// <summary>Remarks nodes</summary>
		/// <remarks>Use `XmlAnyElement` to allow self closing tags with `XmlNode[]`.</remarks>
		[XmlAnyElement("remarks")]
		public XmlNode[] remarksNodes { get; set; }

		/// <summary>Remarks text</summary>
		[XmlIgnore]
		public string remarks => XmlDocHelper.ConvertXmlNodesToText(remarksNodes);

		/// <summary>Example nodes</summary>
		/// <remarks>Use `XmlAnyElement` to allow self closing tags with `XmlNode[]`.</remarks>
		[XmlAnyElement("example")]
		public XmlNode[] exampleNodes { get; set; }

		/// <summary>Example Text</summary>
		[XmlIgnore]
		public string example => XmlDocHelper.ConvertXmlNodesToText(exampleNodes);

	}

	#endregion
}
