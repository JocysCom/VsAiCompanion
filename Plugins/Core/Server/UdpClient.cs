using System.Linq;
using System.Net;
using System.Net.Sockets;

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
