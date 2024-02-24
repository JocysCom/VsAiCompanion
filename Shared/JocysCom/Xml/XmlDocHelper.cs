using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

		public static string GetSummaryText(MemberInfo mi)
		{
			var member = GetMemberDoc(mi);
			// Get <summary> of the property first.
			var text = member?.summary ?? "";
			// If not set then...
			if (!string.IsNullOrEmpty(text))
				return text;
			if (mi.MemberType == MemberTypes.Property)
			{
				//	Get class  summary.
				var pi = mi as PropertyInfo;
				text = GetSummary(pi.PropertyType);
			}
			return text;
		}

		public static string GetExampleText(MemberInfo mi)
		{
			var member = GetMemberDoc(mi);
			var text = member?.example ?? "";
			// If not set then...
			if (!string.IsNullOrEmpty(text))
				return text;
			if (mi.MemberType == MemberTypes.Property)
			{
				//	Get class  summary.
				var pi = mi as PropertyInfo;
				text = GetSummary(pi.PropertyType);
			}
			return text;
		}


		public static string GetParamText(MethodInfo mi, ParameterInfo pi)
		{
			var member = GetMemberDoc(mi);
			var text = member?.param.Where(x => x.name == pi.Name).Select(x => x.value).FirstOrDefault() ?? "";
			if (!string.IsNullOrEmpty(text))
				return text;
			// Get return class summary.
			text = GetSummary(pi.ParameterType);
			return text;
		}

		public static string GetReturnText(MethodInfo mi)
		{
			var member = GetMemberDoc(mi);
			var text = member?.returns.Select(x => x.value).FirstOrDefault() ?? "";
			if (!string.IsNullOrEmpty(text))
				return text;
			// Get return class summary.
			text = GetSummary(mi.ReturnType);
			return text;
		}

		public static string GetSummary(Type type)
		{
			var ti = type.GetTypeInfo();
			var typeMember = GetMemberDoc(ti);
			var summary = typeMember?.summary ?? "";
			return summary;
		}

		public static readonly Regex RxMultiSpace = new Regex("[ \r\n\t\u00A0]+", RegexOptions.Compiled);

		public static string RemoveSpaces(string s)
		{
			if (string.IsNullOrEmpty(s))
				return s;
			return RxMultiSpace.Replace(s, " ").Trim();
		}

		public static string ConvertXmlNodesToText(params XmlNode[] nodes)
		{
			if (nodes == null)
				return null;
			var result = string.Empty;
			foreach (XmlNode node in nodes)
				result += node.InnerText;
			return result;
		}

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

		/// <summary>
		/// Format the parameter type name.
		/// </summary>
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
		public static XmlDoc GetXmlDoc(Assembly assembly, bool cache = true)
		{
			if (assembly is null)
				return null;
			if (!cache)
				return _GetXmlDoc(assembly);
			// If enumeration then use value as a key, otherwise use type string.
			return XmlDocCache.GetOrAdd(assembly, x => _GetXmlDoc(x));
		}

		private static XmlDoc _GetXmlDoc(Assembly assembly)
		{
			var xml = GetXmlDocument(assembly);
			if (xml is null)
				return null;
			//var validator = new Runtime.XmlValidator();
			//validator.IsValid<XmlDoc>(xml.OuterXml, true);
			//var exceptions = validator.Exceptions;
			return Runtime.Serializer.DeserializeFromXml<XmlDoc>(xml);
		}

		/// <summary>Get XML Document.</summary>
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

	}

	#region Helper Classes

	public class XmlDocName
	{
		public string type;
		public string name;
	}

	[Serializable]
	[XmlType(AnonymousType = true)]
	public class XmlDocAssembly
	{
		public string name { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("doc")]
	public class XmlDoc
	{
		public XmlDocAssembly assembly;

		[XmlArrayItem("member")]
		public List<XmlDocMember> members;

	}

	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("param")]
	public partial class XmlDocParam
	{
		[XmlAttribute]
		public string name;

		[XmlText]
		public string value { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot("member")]
	public class XmlDocMember
	{
		[XmlAttribute]
		public string name { get; set; }

		[XmlElement("summary")]
		public XmlNode[] summaryNodes { get; set; }

		public string summary => XmlDocHelper.ConvertXmlNodesToText(summaryNodes);

		[XmlElement("returns")]
		public List<XmlDocParam> returns { get; set; }

		[XmlElement("remarks")]
		public List<XmlDocParam> remarks { get; set; }

		public string example => XmlDocHelper.ConvertXmlNodesToText(exampleNodes);

		[XmlElement("example")]
		public XmlNode[] exampleNodes { get; set; }

		[XmlElement("param")]
		public List<XmlDocParam> param { get; set; }

		[XmlElement("paramrefs")]
		public List<XmlDocParam> paramrefs { get; set; }

		[XmlElement("include")]
		public List<XmlDocParam> includes { get; set; }

		[XmlElement("exceptions")]
		public List<XmlDocParam> exceptions { get; set; }

		[XmlElement("permitions")]
		public List<XmlDocParam> permitions { get; set; }

	}

	#endregion

}
