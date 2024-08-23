using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Server
{
	/// <summary>
	/// UdpClient facilitates invoking remote methods via UDP on a server instantiated with <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">Type defining the methods accessible on the server.</typeparam>
	/// <example>
	/// Usage:
	/// <code>
	/// var UdpClient = new UdpClient&lt;YourClassType&gt;(IPAddress.Loopback, 12002);
	/// var result = UdpClient.CallMethod&lt;string&gt;("YourMethodName", "param1", 123, true);
	/// Console.WriteLine(result);
	/// </code>
	/// </example>
	public class UdpClient<T>
	{
		private readonly UdpClient _udpClient;
		private IPEndPoint _serverEndpoint;

		/// <summary>
		/// Initializes an instance of UdpClient to connect and invoke methods on a server.
		/// </summary>
		/// <param name="serverAddress">Server's IP address.</param>
		/// <param name="serverPort">Port number on which the server is listening.</param>
		public UdpClient(IPAddress serverAddress, ushort? serverPort = null)
		{
			_udpClient = new UdpClient();
			_serverEndpoint = new IPEndPoint(
				serverAddress ?? UdpHelper.DefaultIPAddress,
				serverPort ?? UdpHelper.DefaultStartPort);
		}

		/// <summary>
		/// Scan for servers and return port/title by calling special function.
		/// </summary>
		/// <returns></returns>
		public Dictionary<ushort, string> ScanServers()
		{
			// Dictionary to hold port and server info.
			var servers = new ConcurrentDictionary<ushort, string>();
			// Get the default start and end ports for scanning.
			var startPort = UdpHelper.DefaultStartPort;
			var endPort = UdpHelper.DefaultEndPort;
			// Generate the list of ports to scan.
			var ports = Enumerable.Range(startPort, endPort - startPort + 1).Select(port => (ushort)port).ToList();
			// Use Parallel.ForEach to scan ports concurrently.
			Parallel.ForEach(ports, port =>
			{
				// Create an endpoint for the current port.
				var endPoint = new IPEndPoint(UdpHelper.DefaultIPAddress, port);
				// Prepare the UDP client to send a request.
				using (var udpClient = new UdpClient())
				{
					try
					{
						// Serialize the request to get the process info.
						var requestData = UdpHelper.Serialize(new object[] { nameof(UdpHelper.GetProcessInfo) });
						// Send the request to the server.
						udpClient.Send(requestData, requestData.Length, endPoint);
						// Set a 1 second timeout for receiving the response.
						udpClient.Client.ReceiveTimeout = 1000;
						// Try to receive the response from the server.
						var responseData = udpClient.Receive(ref endPoint);
						// Deserialize the response to get the server info.
						var serverInfo = UdpHelper.Deserialize<string>(responseData);
						// Add the port and server info to the dictionary.
						servers.TryAdd(port, serverInfo);
					}
					catch (Exception)
					{
						// Ignore any exceptions (e.g., timeout, no response) for this port.
					}
				}
			});
			return servers.ToDictionary(kv => kv.Key, kv => kv.Value);
		}

		/// <summary>
		/// Invokes a specific method on the server and retrieves the result.
		/// </summary>
		/// <typeparam name="TResult">Expected return type of the method.</typeparam>
		/// <param name="methodName">Name of the method to invoke.</param>
		/// <param name="parameters">Parameters for the method call.</param>
		/// <returns>Result from the method invocation.</returns>
		public TResult CallMethod<TResult>(string methodName, params object[] parameters)
		{
			// Combine method name and parameters into a single array
			var request = new object[] { methodName }.Concat(parameters).ToArray();
			var requestData = UdpHelper.Serialize(request);
			_udpClient.Send(requestData, requestData.Length, _serverEndpoint);
			var responseData = _udpClient.Receive(ref _serverEndpoint);
			return UdpHelper.Deserialize<TResult>(responseData);
		}
	}
}
