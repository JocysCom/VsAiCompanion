using System.IO;
using System.Reflection;

namespace JocysCom.VS.AiCompanion.DataClient
{
	public static class ResourceHelper
	{

		/// <summary>
		/// Find resource in all loaded assemblies if not specified by full or partial (EndsWith) name.
		/// Look inside "Build Action: Embedded Resource".
		/// </summary>
		public static string FindResource(string name, params Assembly[] assemblies)
		{
			var name1 = name.Replace("/", ".").Replace(@"\", ".");
			var name2 = name1.Replace(' ', '_');
			if (assemblies.Length == 0)
				assemblies = new Assembly[] { Assembly.GetExecutingAssembly() };
			foreach (var assembly in assemblies)
			{
				var resourceNames = assembly.GetManifestResourceNames();
				foreach (var resourceName in resourceNames)
				{
					if (!resourceName.EndsWith(name1) && !resourceName.EndsWith(name2))
						continue;
					var stream = assembly.GetManifestResourceStream(resourceName);
					var streamReader = new StreamReader(stream, true);
					return streamReader.ReadToEnd();
				}
			}
			return default;
		}

	}
}
