using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core.Server;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
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
		public static async void API_LoadPlugins(string pluginsDirectory)
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
				var json = System.IO.File.ReadAllText(aiPluginPath);
				var aiPlugin = Client.Deserialize<ai_plugin>(json);
				// Check for web server exe file.
				var exeFI = pluginDi.GetFileSystemInfos("*.exe").FirstOrDefault();
				int port = 0;
				if (exeFI != null)
				{
					try
					{
						port = API_StartServer(exeFI.FullName);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine(ex.ToString());
						continue;
					}
				}
				// Get OpenAI specificaton file.
				var client = new HttpClient();
				var openApiSpecPath = $"http://localhost:{port}{aiPlugin.api.url}";
				var openApiSpec = await client.GetStringAsync(openApiSpecPath);
				var doc = LoadOpenApiSpec(openApiSpec);
				var pluginItems = ExtractPluginItems(doc);
				API_StopServer(exeFI.FullName);
			}
		}

		public static Dictionary<int, Process> servers = new Dictionary<int, Process>();

		public static int API_StartServer(string executablePath)
		{
			var port = UdpHelper.FindFreePort();
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = executablePath,
					Arguments = $"--urls=http://localhost:{port}",
					UseShellExecute = false,
					//CreateNoWindow = true
				}
			};
			process.Start();
			Task.Delay(4000).Wait();
			servers.Add(port, process);
			return port;
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


		#region OpenAPI

		public static OpenApiDocument LoadOpenApiSpec(string openApiSpec)
		{
			var openApiStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(openApiSpec));
			var openApiDocument = new OpenApiStreamReader().Read(openApiStream, out var diagnostic);
			if (diagnostic.Errors.Any())
			{
				throw new InvalidOperationException("Failed to parse OpenAPI specification.");
			}
			return openApiDocument;
		}

		public static List<PluginItem> ExtractPluginItems(OpenApiDocument openApiDocument)
		{
			var list = new List<PluginItem>();
			foreach (var path in openApiDocument.Paths)
			{
				string pathUrl = path.Key;
				var operations = path.Value.Operations;
				foreach (var operation in operations)
				{
					var item = new PluginItem(openApiDocument, operation.Value);
					string methodType = operation.Key.ToString();
					string operationId = operation.Value.OperationId;
					string description = operation.Value.Summary ?? operation.Value.Description;
					Console.WriteLine($"Path: {pathUrl}, Method: {methodType}");
					Console.WriteLine($"Operation ID: {operationId}");
					Console.WriteLine($"Description: {description}");
					// Extract parameters
					foreach (var parameter in operation.Value.Parameters)
					{
						Console.WriteLine($"Parameter: {parameter.Name}, Type: {parameter.Schema?.Type}, Description: {parameter.Description}");
					}
					list.Add(item);
				}
			}
			return list;
		}

		#endregion

	}
}
