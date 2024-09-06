using JocysCom.VS.AiCompanion.Plugins.Core.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class PluginsManager
	{

		/// <summary>
		/// Load Microsoft.NET.Sdk.Web libraries and include all public API methods from classes tagged with the[ApiController] attribute.
		/// </summary>
		/// <param name="path">Path to the folder with DLLs.</param>
		public static void API_LoadPlugins(string pluginsDirectory)
		{

			// Load all EXE servers in the Plugins directory
			var assemblies = new List<Assembly>();
			foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
			{
				var pluginDi = new DirectoryInfo(pluginDir);
				// Check for "ai-plugin.json" file.
				var aiPluginPath = Path.Combine(pluginDir, ".well-known", "ai-plugin.json");
				if (!File.Exists(aiPluginPath))
					continue;
				// Read "ai-plugin.json" file.
				// ...
				// Check for web server exe file.
				var exeFI = pluginDi.GetFileSystemInfos(".exe").FirstOrDefault();
				if (exeFI != null)
				{
					try
					{
						API_StartServer(exeFI.FullName);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine(ex.ToString());
						continue;
					}
				}
				// Get OpenAI specificaton file. 
			}

		}

		public static Dictionary<int, Process> servers = new Dictionary<int, Process>();

		public static void API_StartServer(string executablePath)
		{
			var port = UdpHelper.FindFreePort();
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = executablePath,
					Arguments = $"--urls=http://localhost:{port}",
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			servers.Add(port, process);
		}

		public static void API_StopServer(string executablePath)
		{
			servers.FirstOrDefault(x => x.Value.StartInfo.FileName == executablePath).Value?.Kill();
		}

		private HttpClient _client = new HttpClient();

		public async Task<string> API_GetSpecification(int port)
		{
			var response = await _client.GetStringAsync($"http://localhost:{port}/swagger/v1/swagger.json");
			return response;
		}

		/// <summary>
		/// Add all methods of the type.
		/// </summary>
		private static void API_AddMethods(string openApiSpecification)
		{
			//var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			//var methods = type.GetMethods(bindingFlags);
			//foreach (var mi in methods)
			//{
			//	var attribute = mi.GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == nameof(RiskLevelAttribute));
			//	if (attribute is null)
			//		continue;
			//	// Get level from attribute using reflection to access Level property
			//	var levelProperty = attribute.GetType().GetProperty(nameof(RiskLevelAttribute.Level));
			//	if (levelProperty != null)
			//	{
			//		var levelValue = (RiskLevel)levelProperty.GetValue(attribute);
			//		if (levelValue > RiskLevel.Unknown)
			//			_PluginFunctions.Add(mi.Name, mi);
			//	}
			//}
		}

		public async Task CallMethod(int port, string url)
		{
			var _client = new HttpClient();
			var content = new StringContent($"{{\"url\": \"{url}\"}}", System.Text.Encoding.UTF8, "application/json");
			var response = await _client.PostAsync($"http://localhost:{port}/LinkReader/execute", content);
			var result = await response.Content.ReadAsStringAsync();
			Console.WriteLine($"Response: {result}");
		}

	}
}
