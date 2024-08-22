using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Server
{
	/// <summary>
	/// UdpServer establishes a simple UDP-based RPC server for invoking methods defined in the specified type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">Class type with methods to expose via the server.</typeparam>
	/// <example>
	/// Usage:
	/// <code>
	/// var UdpServer = new UdpServer&lt;YourClassType&gt;();
	/// UdpServer.StartServer(IPAddress.Loopback, 12002);
	/// UdpServer.StopServer();
	/// </code>
	/// </example>
	public class UdpServer<T> where T : new()
	{
		private UdpClient _udpClient;
		private IPEndPoint _endPoint;
		private bool _isRunning;
		private T _instance = new T();

		/// <summary>
		/// Starts the server to listen for incoming UDP requests on the given address and port.
		/// </summary>
		/// <param name="address">IP Address to bind the server.</param>
		/// <param name="port">Port number to listen on.</param>
		public void StartServer(IPAddress address = null, int? port = null)
		{
			var serverPort = port ?? UdpHelper.FindFreePort(
				UdpHelper.DefaultStartPort,
				UdpHelper.DefaultEndPort);
			_endPoint = new IPEndPoint(address ?? IPAddress.Loopback, serverPort);
			_udpClient = new UdpClient(serverPort);
			_isRunning = true;
			Task.Run(() => Listen());
			Console.WriteLine("Server started...");
		}

		/// <summary>
		/// Stops the server from listening for incoming requests.
		/// </summary>
		public void StopServer()
		{
			_isRunning = false;
			_udpClient?.Close();
			Console.WriteLine("Server stopped.");
		}

		/// <summary>
		/// Listens for incoming UDP requests and processes them by invoking corresponding methods.
		/// </summary>
		private async Task Listen()
		{
			while (_isRunning)
			{
				try
				{
					var result = await _udpClient.ReceiveAsync();
					var data = result.Buffer;
					var request = UdpHelper.Deserialize<object[]>(data);
					var methodName = request[0] as string;
					var parameters = request.Skip(1).ToArray();
					var method = typeof(T).GetMethod(methodName);
					if (method == null)
					{
						if (methodName == nameof(GetProcessInfo))
						{
							var response = GetProcessInfo();
							var responseData = UdpHelper.Serialize(response);
							await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
						}
						else
						{
							throw new InvalidOperationException($"Method {methodName} not found.");
						}
					}
					else
					{
						var response = method.Invoke(_instance, parameters);
						var responseData = UdpHelper.Serialize(response);
						await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Returns the title of the current process.
		/// </summary>
		/// <returns>Title of the current process.</returns>
		public static string GetProcessInfo()
		{
			return Process.GetCurrentProcess().MainWindowTitle;
		}

	}
}
