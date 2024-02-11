//// Originaly created by
////     Stephen Toub [stoub@microsoft.com]
//// Modified by: 
////     Evaldas Jocys [evaldas@jocys.com]
////
//// XmlComments.cs
//// Retrieve the xml comments stored in the assembly's comments file
//// for specific types or members of types.
//using System;
//using System.IO;
//using System.Xml;
//using System.Text;
//using System.Reflection;
//using System.Diagnostics;
//using System.Collections;
//using System.Runtime.InteropServices;

//namespace JocysCom.ClassLibrary.Xml
//{
//    public partial class XmlComments
//    {
//        #region Member Variables
//        /// <summary>The entire XML comment block for this member.</summary>
//        private XmlNode _comments;
//        /// <summary>The summary comment for this member.</summary>
//        private XmlNode _summary;
//        /// <summary>The remarks comment for this member.</summary>
//        private XmlNode _remarks;
//        /// <summary>The return comment for this member.</summary>
//        private XmlNode _return;
//        /// <summary>The value comment for this member.</summary>
//        private XmlNode _value;
//        /// <summary>The example comment for this member.</summary>
//        private XmlNode _example;
//        /// <summary>The includes comments for this member.</summary>
//        private XmlNodeList _includes;
//        /// <summary>The exceptions comments for this member.</summary>
//        private XmlNodeList _exceptions;
//        /// <summary>The paramrefs comments for this member.</summary>
//        private XmlNodeList _paramrefs;
//        /// <summary>The permissions comments for this member.</summary>
//        private XmlNodeList _permissions;
//        /// <summary>The params comments for this member.</summary>
//        private XmlNodeList _params;
//        #endregion

//        #region Extracting Specific Comments
//        /// <summary>Gets the entire XML comment block for this member.</summary>
//        public XmlNode AllComments { get { return _comments; } }
//        /// <summary>Gets the summary comment for this member.</summary>
//        public XmlNode Summary { get { return _summary; } }
//        /// <summary>Gets the remarks comment for this member.</summary>
//        public XmlNode Remarks { get { return _remarks; } }
//        /// <summary>Gets the return comment for this member.</summary>
//        public XmlNode Return { get { return _return; } }
//        /// <summary>Gets the value comment for this member.</summary>
//        public XmlNode Value { get { return _value; } }
//        /// <summary>Gets the example comment for this member.</summary>
//        public XmlNode Example { get { return _example; } }
//        /// <summary>Gets the includes comments for this member.</summary>
//        public XmlNodeList Includes { get { return _includes; } }
//        /// <summary>Gets the exceptions comments for this member.</summary>
//        public XmlNodeList Exceptions { get { return _exceptions; } }
//        /// <summary>Gets the paramrefs comments for this member.</summary>
//        public XmlNodeList ParamRefs { get { return _paramrefs; } }
//        /// <summary>Gets the permissions comments for this member.</summary>
//        public XmlNodeList Permissions { get { return _permissions; } }
//        /// <summary>Gets the params comments for this member.</summary>
//        public XmlNodeList Params { get { return _params; } }
//        /// <summary>Renders to a string the entire XML comment block for this member.</summary>
//        public override string ToString() { return _comments.OuterXml; }
//        #endregion

//        #region Init Comments

//        private void InitComments()
//        {
//            if (_comments != null)
//            {
//                // Get single nodes (comments that can appear only once)
//                _summary = _comments.SelectSingleNode(Tags.Summary);
//                _return = _comments.SelectSingleNode(Tags.Returns);
//                _remarks = _comments.SelectSingleNode(Tags.Remarks);
//                _example = _comments.SelectSingleNode(Tags.Example);
//                _value = _comments.SelectSingleNode(Tags.Value);
//                // Get node lists (comments that can appear multiple times)
//                _includes = _comments.SelectNodes(Tags.Include);
//                _exceptions = _comments.SelectNodes(Tags.Exception);
//                _paramrefs = _comments.SelectNodes(SubTags.ParamRef);
//                _permissions = _comments.SelectNodes(Tags.Permission);
//                _params = _comments.SelectNodes(Tags.Param);
//            }
//            else
//            {
//                // Make it easier for people to use this class when no comments exist
//                // by creating dummy nodes for all properties.
//                _comments = new XmlDocument();
//                _summary = _return = _remarks = _example = _value = _comments;
//                _includes = _exceptions = _paramrefs = _permissions = _params = _comments.ChildNodes;
//            }
//        }
		
//    #endregion

//        #region Parameters

//        /// <summary>Generates a parameter string used when searching xml comment files.</summary>
//        /// <param name="parameters">List of parameters to a member.</param>
//        /// <returns>A parameter string used when searching xml comment files.</returns>
//        private static string CreateParamsDescription(System.Collections.Generic.Dictionary<string, Type> parameters)
//        {
//            StringBuilder paramDesc = new StringBuilder();

//            // If there are parameters then we need to construct a list
//            string[] keys = new string[parameters.Keys.Count];
//            parameters.Keys.CopyTo(keys, 0);
//            if (keys.Length > 0)
//            {
//                // Start the list
//                paramDesc.Append("(");
//                // For each parameter, append the type of the parameter.
//                // Separate all items with commas.
//                for (int i = 0; i < keys.Length; i++)
//                {
//                    string key = keys[i];
//                    Type paramType = parameters[key];
//                    string paramName = key;
//                    // Handle special case where ref parameter ends in & but xml docs use @.
//                    // Pointer parameters end in * in both type representation and xml comments representation.
//                    if (paramName.EndsWith("&")) paramName = paramName.Substring(0, paramName.Length - 1) + "@";

//                    // Handle multidimensional arrays
//                    if (paramType.IsArray && paramType.GetArrayRank() > 1)
//                    {
//                        paramName = paramName.Replace(",", "0:,").Replace("]", "0:]");
//                    }

//                    // Append the fixed up parameter name
//                    paramDesc.Append(paramName);
//                    if (i != parameters.Keys.Count - 1) paramDesc.Append(",");
//                }

//                // End the list
//                paramDesc.Append(")");
//            }
//            // Return the parameter list description
//            return paramDesc.ToString();
//        }
//        #endregion

//        #region XML to String
		
//        /// <summary>
//        /// Convert XML comments to string.
//        /// </summary>
//        /// <returns></returns>
//        public string ToComments() {
//            return ToComments("", false);
//        }

//        /// <summary>
//        /// Convert XML comments to string.
//        /// </summary>
//        /// <param name="prefix">Prefix to add at start of each line.</param>
//        /// <param name="contentOnly">Extract comments content otherwise include member node.</param>
//        /// <returns>XML Documentation string</returns>
//        /// <example>
//        /// <code>.ToComments("--- ", true);</code>
//        /// </example>
//        public string ToComments(string prefix, bool contentOnly)
//        {
//            return ToComments(prefix, contentOnly, true);
//        }

//        /// <summary>
//        /// Convert XML comments to string.
//        /// </summary>
//        /// <param name="prefix">Prefix to add at start of each line.</param>
//        /// <param name="contentOnly">Extract comments content otherwise include member node.</param>
//        /// <param name="includeEmptyNodes">Include empty nodes.</param>
//        /// <returns>XML Documentation string</returns>
//        /// <example>
//        /// <code>.ToComments("--- ", true, true);</code>
//        /// </example>
//        public string ToComments(string prefix, bool contentOnly, bool includeEmptyNodes)
//        {
//            // We can remove summary node only if all nodes are empty or
//            // .NET compiler will fail to generate proper documentation.
//            bool keepSummaryNode = AllComments.InnerText.Length > 0;
//            // We need to format XML comments.
//            StringBuilder sb = new StringBuilder();
//            StringWriter sw = new StringWriter(sb);
//            XmlTextWriter xw = new XmlTextWriter(sw);
//            xw.Formatting = System.Xml.Formatting.Indented;
//            // Write formated XML to xw -> sw -> sb.
//            if (contentOnly)
//            {
//                foreach (XmlNode node in AllComments.ChildNodes)
//                {
//                    if (!String.IsNullOrEmpty(node.InnerText) || includeEmptyNodes
//                        || (node.Name == Tags.Summary && keepSummaryNode))
//                    {
//                        node.WriteTo(xw);
//                    }
//                }
//            }
//            else
//            {
//                AllComments.WriteTo(xw);
//            }
//            xw.Flush();
//            xw.Close();
//            string s = sb.ToString();
//            // Regex line start "^" will have one match even is string is empty.
//            // So use regex only if comments XML string is not empty.
//            if (s.Length > 0)
//            {
//                // Insert prefix at start of each line.
//                System.Text.RegularExpressions.Regex lineStart;
//                lineStart = new System.Text.RegularExpressions.Regex("^", System.Text.RegularExpressions.RegexOptions.Multiline);
//                s = lineStart.Replace(s, prefix);
//            }
//            return s;
//        }


//        #endregion

//    }
//}
