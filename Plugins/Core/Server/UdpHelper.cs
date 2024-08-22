using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Server
{
	/// <summary>
	/// UDP Helper.
	/// </summary>
	public class UdpHelper
	{

		/// <summary>
		/// Defaul range start server port.
		/// </summary>
		public static ushort DefaultStartPort { get; set; } = 12000;

		/// <summary>
		/// Defaul range end server port.
		/// </summary>
		public static ushort DefaultEndPort { get; set; } = 12010;

		/// <summary>
		/// Defaul IP Address. Loopback IP limits communication to local machine.
		/// </summary>
		public static IPAddress DefaultIPAddress = IPAddress.Loopback;

		/// <summary>
		/// Find first free port. IANA Port categories:
		///         0 –  1023 – System or Well Known ports.
		///      1024 – 49151 – User or Registered ports.
		///     49152 - 65535 – Dynamic (Private) or Ephemeral Ports.
		/// </summary>
		/// <returns>Free port number if found; otherwise 0.</returns>
		public static int FindFreePort(ushort startPort = 49152, ushort endPort = ushort.MaxValue)
		{
			var portArray = new List<int>();
			var properties = IPGlobalProperties.GetIPGlobalProperties();
			// Get TCP connection ports.
			var ports = properties.GetActiveTcpConnections()
				.Where(x => x.LocalEndPoint.Port >= startPort)
				.Select(x => x.LocalEndPoint.Port);
			portArray.AddRange(ports);
			// Get TCP listener ports.
			ports = properties.GetActiveTcpListeners()
				.Where(x => x.Port >= startPort)
				.Select(x => x.Port);
			portArray.AddRange(ports);
			// Get UDP listener ports.
			ports = properties.GetActiveUdpListeners()
				.Where(x => x.Port >= startPort)
				.Select(x => x.Port);
			portArray.AddRange(ports);
			// Get first port not in the list.
			for (int i = startPort; i <= endPort; i++)
				if (!portArray.Contains(i))
					return i;
			return 0;
		}

		/// <summary>
		/// Serialize request.
		/// </summary>
		public static byte[] Serialize(object o)
		{
			var bytes = JsonSerializer.SerializeToUtf8Bytes(o);
			if (EnableEncryption)
				bytes = UserEncrypt(bytes);
			return bytes;
		}

		/// <summary>
		/// Deserialize response.
		/// </summary>
		public static T Deserialize<T>(byte[] bytes)
		{
			if (EnableEncryption)
				bytes = UserDecrypt(bytes);
			return JsonSerializer.Deserialize<T>(bytes);
		}

		/// <summary>
		/// Enable encrytion. Encryptuon limits communication to the same user only.
		/// </summary>
		public static bool EnableEncryption = true;

		/// <summary>
		/// Default salt for encryption and decryption.
		/// </summary>
		public static string CryptoSalt = nameof(UdpHelper);

		/// <summary>
		/// Encrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static byte[] UserEncrypt(byte[] data)
		{
			if (data == null)
				return null;
			try
			{
				var salt = CryptoSalt;
				return JocysCom.ClassLibrary.Security.Encryption.Encrypt(data, salt);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		/// <summary>
		/// Decrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static byte[] UserDecrypt(byte[] data)
		{
			if (data == null)
				return null;
			try
			{
				var salt = CryptoSalt;
				return JocysCom.ClassLibrary.Security.Encryption.Decrypt(data, salt);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

	}
}
