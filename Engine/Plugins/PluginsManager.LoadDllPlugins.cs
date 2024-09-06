using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
#if NETFRAMEWORK
#else
using System.Runtime.Loader;
#endif

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class PluginsManager
	{
		/// <summary>
		/// Load Microsoft.NET.Sdk.Web libraries and include all public API methods from classes tagged with the[ApiController] attribute.
		/// </summary>
		/// <param name="path">Path to the folder with DLLs.</param>
		public static void LoadDllPlugins(string pluginsDirectory)
		{
#if NETFRAMEWORK
#else
			var loadContext = new AssemblyLoadContext("PluginContext", true);
			//AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			loadContext.Resolving += (context, name) =>
			{
				var assemblyFile = $"{new AssemblyName(name.Name).Name}.dll";
				var assemblyPath = Path.Combine(pluginsDirectory, assemblyFile);
				if (File.Exists(assemblyPath))
					return context.LoadFromAssemblyPath(assemblyPath);

				// Attempt to load the assembly from globally known paths or cache directories
				string[] possiblePaths = new string[]
				{
					Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFile),
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
					 @"dotnet\shared\Microsoft.AspNetCore.App\" + assemblyFile)
					// Add more standard paths if known
				};
				foreach (var path in possiblePaths)
				{
					if (File.Exists(path))
						return Assembly.LoadFrom(path);
				}
				return null;
			};
#endif
			// Load all DLLs within the Plugins directory
			var assemblies = new List<Assembly>();
			foreach (var directory in Directory.GetDirectories(pluginsDirectory))
			{
				foreach (var dll in Directory.GetFiles(directory, "*.dll"))
				{
					try
					{
#if NETFRAMEWORK
						var assembly = Assembly.LoadFrom(dll);
#else
						var assembly = loadContext.LoadFromAssemblyPath(dll);
#endif
						assemblies.Add(assembly);
					}
					catch (Exception ex)
					{
						// Handle or log exceptions such as bad format, file not found, etc.
						Console.WriteLine($"Could not load assembly {dll}: {ex.Message}");
						continue;
					}
				}
			}
			// Create a configuration
			//var configuration = new ContainerConfiguration().WithAssemblies(assemblies);
			foreach (var assembly in assemblies)
			{
				// Discover methods marked with a custom attribute using reflection
				var types = assembly.GetTypes();
				foreach (var type in types)
				{
					try
					{
						AddMethods(type);
					}
					catch (Exception ex)
					{
						// Handle or log exceptions such as bad format, file not found, etc.
						Console.WriteLine($"Could not load type {ex.Message}");
						continue;
					}
				}
			}

		}

	}
}
