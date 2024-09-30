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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class PluginsManager
	{

		class NativeMethods
		{
			// P/Invoke declarations
			[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
			public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

			[DllImport("kernel32.dll")]
			public static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

			[DllImport("kernel32.dll")]
			public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern bool CloseHandle(IntPtr hObject);

			[StructLayout(LayoutKind.Sequential)]
			public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
			{
				public long PerProcessUserTimeLimit;
				public long PerJobUserTimeLimit;
				public uint LimitFlags;
				public IntPtr MinimumWorkingSetSize;
				public IntPtr MaximumWorkingSetSize;
				public uint ActiveProcessLimit;
				public ulong Affinity;
				public uint PriorityClass;
				public uint SchedulingClass;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct IO_COUNTERS
			{
				public ulong ReadOperationCount;
				public ulong WriteOperationCount;
				public ulong OtherOperationCount;
				public ulong ReadTransferCount;
				public ulong WriteTransferCount;
				public ulong OtherTransferCount;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
			{
				public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
				public IO_COUNTERS IoInfo;
				public IntPtr ProcessMemoryLimit;
				public IntPtr JobMemoryLimit;
				public IntPtr PeakProcessMemoryUsed;
				public IntPtr PeakJobMemoryUsed;
			}

			public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
			public const int JobObjectExtendedLimitInformation = 9;

			public static IntPtr GetJobHandle()
			{
				// Create a job object
				var jobHandle = CreateJobObject(IntPtr.Zero, null);
				// Set job object limits to terminate processes when the parent process exits
				var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
				info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
				int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
				IntPtr infoPtr = Marshal.AllocHGlobal(length);
				Marshal.StructureToPtr(info, infoPtr, false);
				SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, infoPtr, (uint)length);
				Marshal.FreeHGlobal(infoPtr);
				return jobHandle;
			}

		}

		/// <summary>
		/// Handle to the job object that groups the processes of the web servers.
		/// </summary>
		private static IntPtr jobHandle;

		/// <summary>
		/// Load Microsoft.NET.Sdk.Web libraries and include all public API methods from classes tagged with the[ApiController] attribute.
		/// </summary>
		/// <param name="path">Path to the folder with DLLs.</param>
		public static async Task API_LoadPlugins(string pluginsDirectory)
		{
			// Create a job object
			jobHandle = NativeMethods.GetJobHandle();
			// Ensure the job object is closed on application exit
			AppDomain.CurrentDomain.ProcessExit += (s, e) => NativeMethods.CloseHandle(jobHandle);

			// Load all EXE servers in the Plugins directory
			var assemblies = new List<Assembly>();
			var di = new DirectoryInfo(pluginsDirectory);
			if (!di.Exists)
				return;
			foreach (var pluginDi in di.GetDirectories())
			{
				// Check for "ai-plugin.json" file.
				var aiPluginPath = Path.Combine(pluginDi.FullName, ".well-known", "ai-plugin.json");
				if (!File.Exists(aiPluginPath))
					continue;
				// Read "ai-plugin.json" file.
				var json = System.IO.File.ReadAllText(aiPluginPath);
				var aiPlugin = Client.Deserialize<ai_plugin>(json);
				// Check for web server exe file.
				var exeFI = pluginDi.GetFileSystemInfos("*.exe").FirstOrDefault();
				Uri openApiSpecUri = null;
				AiService service = null;
				try
				{
					var url = aiPlugin.api.url;
					var uri = Uri.IsWellFormedUriString(url, UriKind.Absolute)
						? url
						: $"http://localhost/{url.TrimStart('/')}";
					openApiSpecUri = new Uri(uri);

					var services = Global.AppSettings.AiServices;
					service = services
						.Where(x => string.Equals(url, x.BaseUrl, StringComparison.OrdinalIgnoreCase))
						.FirstOrDefault();
					if (service is null)
					{
						service = new AiService();
						service.ServiceType = ApiServiceType.AiPlugin;
						service.BaseUrl = openApiSpecUri.GetLeftPart(UriPartial.Authority);
						service.Name = openApiSpecUri.Host;
						services.Add(service);
					}
					if (Global.AppSettings.EnableApiPlugins && exeFI != null)
					{
						var kv = await API_StartServer(exeFI.FullName);
						var ub = new UriBuilder(uri);
						ub.Host = kv.uri.Host;
						ub.Port = kv.uri.Port;
						openApiSpecUri = ub.Uri;
						servers.Add(ub.Uri, kv.process);
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.ToString());
				}

				if (openApiSpecUri is null)
					continue;
				if (Global.AppSettings.EnableApiPlugins && service?.IsEnabled == true)
				{
					// Get OpenAI specificaton file.
					var client = new HttpClient();
					var openApiSpec = await client.GetStringAsync(openApiSpecUri.ToString());
					var doc = LoadOpenApiSpec(openApiSpec);
					var pluginItems = ExtractPluginItems(doc);
					_PluginFunctions.AddRange(pluginItems);
					//API_StopServer(exeFI.FullName);
				}
			}
		}

		public static Dictionary<Uri, Process> servers = new Dictionary<Uri, Process>();

		private static async Task<Exception> WaitForServerToStartAsync(Uri uri, int timeoutMilliseconds = 30000)
		{
			var httpClient = new HttpClient();
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(timeoutMilliseconds);
			while (true)
			{
				try
				{
					var response = await httpClient.GetAsync(uri, cancellationTokenSource.Token);
					//if (response.IsSuccessStatusCode)
					if (response != null)
						break;
				}
				catch (TaskCanceledException tcex)
				{
					return tcex;
				}
				catch (Exception ex)
				{
					return ex;
				}
				await Task.Delay(500, cancellationTokenSource.Token);
			}
			httpClient.Dispose();
			return null;
		}

		public async static Task<(Uri uri, Process process)> API_StartServer(string executablePath)
		{
			var port = UdpHelper.FindFreePort();
			var uri = new Uri($"http://localhost:{port}");
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = executablePath,
					Arguments = $"--urls={uri}",
					UseShellExecute = false,
					WindowStyle = ProcessWindowStyle.Minimized,
					//CreateNoWindow = true
				},
				EnableRaisingEvents = true // Enable raising events for the process
			};
			process.Exited += Process_Exited;
			process.Start();
			// Assign the process to the job object.
			// This will make sure that the process is killed when the parent process is killed.
			NativeMethods.AssignProcessToJobObject(jobHandle, process.Handle);
			// Wait for the server to start responding
			await WaitForServerToStartAsync(uri);
			return (uri, process);
		}

		public static void API_StopServer(string executablePath)
		{
			var serverEntry = servers.FirstOrDefault(x => x.Value.StartInfo.FileName == executablePath);
			if (serverEntry.Value != null && !serverEntry.Value.HasExited)
			{
				serverEntry.Value.Kill();
				servers.Remove(serverEntry.Key); // Remove from dictionary once killed
			}
		}

		private static void Process_Exited(object sender, EventArgs e)
		{
			var process = (Process)sender;
			Console.WriteLine($"Process {process.Id} has exited.");
		}

		private HttpClient _client = new HttpClient();

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
