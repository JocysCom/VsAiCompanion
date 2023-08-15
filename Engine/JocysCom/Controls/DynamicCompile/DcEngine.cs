using Microsoft.CSharp;
using Microsoft.VisualBasic;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JocysCom.ClassLibrary.Controls.DynamicCompile
{
	public partial class DcEngine
	{
		public DcEngine(string code, LanguageType language = LanguageType.CSharp, string entry = "Main")
		{
			if (string.IsNullOrEmpty(code))
				throw new ArgumentNullException(nameof(code));
			SourceCode = code;
			Language = language;
			EntryPoint = entry;
		}

		private readonly BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public string[] GetEntryPoints(bool includePrivate = false)
		{
			var list = new List<string>();
			if (CurrentAssembly is null)
				return list.ToArray();
			var mods = CurrentAssembly.GetModules(false);
			if (mods.Length == 0)
				return list.ToArray();
			var types = includePrivate
				? mods[0].GetTypes().Where(x => x.IsClass).ToArray()
				: mods[0].GetTypes().Where(x => x.IsClass && x.IsPublic).ToArray();
			if (Language == LanguageType.JScript)
				types = types.Where(x => x.Name != "JScript 0").ToArray();
			foreach (var type in types)
			{
				var methods = type.GetMethods(flags);
				foreach (var method in methods)
					list.Add(string.Format("{0}.{1}", type.Name, method.Name));
			}
			return list.ToArray();
		}

		public Assembly CurrentAssembly
		{
			get
			{
				if (_CurrentAssembly is null)
				{
					var codeToCompile = PrepareCodeToCompile(SourceCode);
					_CurrentAssembly = CreateAssembly(codeToCompile);
				}
				return _CurrentAssembly;
			}
		}
		Assembly _CurrentAssembly;

		public object Run(params object[] parameters)
		{
			ErrorsLog.Clear();
			var results = CallEntry(CurrentAssembly, EntryPoint, parameters);
			return results;
		}

		/// <summary>Main source code that will be dynamically compiled and executed.</summary>
		public readonly string SourceCode;

		/// <summary>Language of the code.</summary>
		public readonly LanguageType Language;

		/// <summary>Method name which will be called.</summary>
		public string EntryPoint;

		// Assemblies that are referenced by the created assembly.
		internal ArrayList References = new ArrayList();


		public StringBuilder ErrorsLog = new StringBuilder();

		public List<string> basicNamespaces = new List<string>()
		{
			"System",
			"System.IO",
			"System.Windows.Forms",
			"System.Drawing",
			"System.Data",
			"System.Threading",
			"System.Xml",
			"System.Collections",
			"System.Diagnostics",
		};


		/// <summary>
		/// Prepare the source code.
		/// </summary>
		private string PrepareCodeToCompile(string originalCode)
		{
			// add some using/Imports statements
			var code = "";
			var namespaces = "";
			var list = basicNamespaces.ToList();
			if (Language == LanguageType.VB)
				list.Add("Microsoft.VisualBasic");
			if (Language == LanguageType.VB || Language == LanguageType.CSharp)
			{
				var lines = list.Select(x => Language == LanguageType.VB
					? string.Format("Imports {0}", x)
					: string.Format("using {0};", x)
				);
				// Get list of missing namespaces.
				var missing = lines
					.Where(x => originalCode.IndexOf(x, StringComparison.OrdinalIgnoreCase) < 0)
					.ToList();
				namespaces = string.Join("\r\n", missing);
				code = namespaces + "\r\n" + originalCode;
			}
			return code;
		}

		// compile the source, and create assembly in memory
		// this method code is mainly from jconwell, 
		// see http://www.codeproject.com/dotnet/DotNetScript.asp
		private Assembly CreateAssembly(string sourceCode)
		{
			if (sourceCode.Length == 0)
			{
				LogError("Error:  There was no CS script code to compile");
				return null;
			}
			// Create compiler.
			CodeDomProvider codeProvider = null;
			switch (Language)
			{
				case LanguageType.CSharp:
					codeProvider = new CSharpCodeProvider();
					break;
				case LanguageType.VB:
					codeProvider = new VBCodeProvider();
					break;
				case LanguageType.JScript:
					// Requires reference to Microsoft.JScript[.dll]
					//codeProvider = new Microsoft.JScript.JScriptCodeProvider();
					break;
				default:
					break;
			}
			//add compiler parameters
			var options = new List<string>();
			if (Language == LanguageType.CSharp || Language == LanguageType.VB)
			{

				options.Add("/target:library");
				options.Add("/errorreport:prompt");
				var entryAsm = Assembly.GetEntryAssembly();
				var refs = new List<AssemblyName>();
				refs.Add(entryAsm.GetName());
				// Load assemblies (DLL files).
				var referenced = Assembly.GetEntryAssembly().GetReferencedAssemblies();
				refs.AddRange(referenced);
				for (var a = 0; a < refs.Count(); a++)
				{
					if (refs[a].Name == "mscorlib")
						continue;
					var refAssembly = Assembly.Load(refs[a].FullName);
					options.Add(string.Format(@"/reference:""{0}""", refAssembly.Location));
				}
				options.Add("/debug+");
				options.Add("/debug:full");
				options.Add("/filealign:512");
				options.Add("/optimize-");
			}
			if (Language == LanguageType.VB)
			{
				options.Add("/define:DEBUG");
			}
			if (Language == LanguageType.CSharp)
			{
				options.Add("/define:DEBUG;TRACE");
				options.Add("/warn:4");
				options.Add("/noconfig");
				options.Add("/nowarn:1701,1702");
			}
			var compilerParams = new CompilerParameters();
			compilerParams.CompilerOptions = string.Join(" ", options);
			compilerParams.GenerateExecutable = false;
			compilerParams.GenerateInMemory = true;
			compilerParams.IncludeDebugInformation = true;
			if (Language == LanguageType.VB)
				compilerParams.ReferencedAssemblies.Add("Microsoft.VisualBasic.dll");
			if (Language == LanguageType.JScript)
				compilerParams.ReferencedAssemblies.Add("Microsoft.JScript.dll");
			//var assemblies = someType.Assembly.GetReferencedAssemblies().ToList();
			//var assemblyLocations = assemblies.Select(a => Assembly.ReflectionOnlyLoad(a.FullName).Location).ToList();
			//assemblyLocations.Add(someType.Assembly.Location);
			//cp.ReferencedAssemblies.AddRange(assemblyLocations.ToArray());
			// Add any additional references needed.
			foreach (string refAssembly in References)
			{
				try
				{
					compilerParams.ReferencedAssemblies.Add(refAssembly);
				}
				catch (Exception ex)
				{
					LogError("Reference Add Error:  " + ex.ToString());
				}
			}
			// Compile the code.
			var results = codeProvider.CompileAssemblyFromSource(compilerParams, sourceCode);
			// If compiling resulted in errors then...
			if (results.Errors.Count > 0)
			{
				foreach (CompilerError error in results.Errors)
					LogError("Compile Error:  " + error.ToString());
				return null;
			}
			// Get a hold of the actual assembly that was generated.
			var compiledAssembly = results.CompiledAssembly;
			return compiledAssembly;
		}

		private object CallEntry(Assembly assembly, string entryPoint, object[] parameters)
		{
			var entry = entryPoint.Split('.');
			if (entry.Length != 2)
				return null;
			var className = entry[0];
			var methodName = entry[1];
			object results = null;
			try
			{
				//Use reflection to call the static Main function
				var mods = assembly.GetModules(false);
				var types = mods[0].GetTypes().Where(x => x.Name == className).ToArray();
				foreach (var type in types)
				{
					var mi = type.GetMethod(methodName, flags);
					if (mi != null)
					{
						var instance = Activator.CreateInstance(type);
						results = mi.GetParameters().Length > 0
							? mi.Invoke(instance, parameters)
							: mi.Invoke(instance, null);
						return results;
					}
				}
				LogError(string.Format("Error: Entry point 'public static {0}' not found!", entryPoint));
			}
			catch (Exception ex)
			{
				LogError("Error: An exception occurred!", ex);
			}
			return results;
		}

		internal void LogError(string message, Exception ex = null)
		{
			// Add message.
			ErrorsLog.AppendLine(message);
			// Add main exceptions and internal exceptions.
			while (ex != null)
			{
				ErrorsLog.Append("\t").AppendLine(ex.ToString());
				ex = ex.InnerException;
			}
		}

	}
}
