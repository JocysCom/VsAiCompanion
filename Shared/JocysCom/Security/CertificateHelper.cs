using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JocysCom.WebSites.Engine.Security
{
	public partial class CertificateHelper
	{
		// Example:
		//
		//// Generate new self signed certificate.
		//var name = "name@domain.com";
		////name = WindowsIdentity.GetCurrent().Name;
		//Console.WriteLine("Generating Certificate for: {0}", name);
		//var properties = new SelfSignedCertProperties(name);
		//var cert = CreateSelfSignedCertificate(properties);
		//var priBin = "Store\\" + name + ".pfx";
		//var pubBin = "Store\\" + name + ".PublicKey.cer";
		//var priPem = "Store\\" + name + ".pem";
		//var pubPem = "Store\\" + name + ".PublicKey.pem";
		//string privateKeyPassword = null;
		//if (cert != null)
		//{
		//	ExportPrivateKey(cert, priBin, false, privateKeyPassword);
		//	ExportPrivateKey(cert, priPem, true, privateKeyPassword);
		//	ExportPublicKey(cert, pubBin, false);
		//	ExportPublicKey(cert, pubPem, true);
		//}
		//// Encryption test.
		//var text = "Test";
		//Console.WriteLine("Encrypt: {0}", text);
		//var encrypted = Encrypt(pubBin, "Test", null, true);
		//Console.WriteLine(encrypted);
		//// Decryption test.
		//var decrypted = Decrypt(priBin, encrypted, privateKeyPassword);
		//Console.WriteLine("Decrypted: {0}", decrypted);
		//Console.WriteLine("Done");

		//// Requires .NET Framework 4.7.2
		//// Namespace: System.Security.Cryptography.X509Certificates
		//// Assemblies: System.Security.Cryptography.X509Certificates.dll
		//
		//public static void GenerateCertificate(string name)
		//{
		//	var rsa = RSA.Create("RSA_2048");
		//	var distinguishedName = new X500DistinguishedName("CN=Message_" + name);
		//	var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		//	var endEntityTypicalUsages =
		//		X509KeyUsageFlags.DataEncipherment |
		//		X509KeyUsageFlags.KeyEncipherment |
		//		X509KeyUsageFlags.DigitalSignature;
		//	request.CertificateExtensions.Add(new X509KeyUsageExtension(endEntityTypicalUsages, true));
		//	request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
		//	var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
		//}

		/// <summary>Export private key as name.PrivateKey.pfx or name.PrivateKey.pem.</summary>
		public static void ExportPrivateKey(X509Certificate2 cert, string fileName, bool ascii = false, string password = null)
		{
			Export(cert, fileName, ascii, true, password);
		}

		/// <summary>Export public key as name.PublicKey.cer or name.PublicKey.pem.</summary>
		public static void ExportPublicKey(X509Certificate2 cert, string fileName, bool ascii = false)
		{
			Export(cert, fileName, ascii);
		}

		static void Export(X509Certificate2 cert, string fileName, bool ascii = false, bool includePrivateKey = false, string privateKeyPassword = null)
		{
			var fi = new FileInfo(fileName);
			if (!fi.Directory.Exists)
				fi.Directory.Create();
			var certType = includePrivateKey ? X509ContentType.Pfx : X509ContentType.Cert;
			var bytes = cert.Export(certType, privateKeyPassword);
			if (ascii)
			{
				var type = includePrivateKey ? "RSA PRIVATE KEY" : "CERTIFICATE";
				var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
				var sb = new StringBuilder();
				sb.AppendLine("-----BEGIN " + type + "-----");
				sb.AppendLine(base64);
				sb.AppendLine("-----END " + type + "-----");
				File.WriteAllText(fileName, sb.ToString(), Encoding.ASCII);
			}
			else
			{
				File.WriteAllBytes(fileName, bytes);
			}
		}

		/// <summary>
		/// Encrypt string to base64 string.
		/// </summary>
		/// <param name="fileName">File name which contains Public key. You can also use the PFX here as it contains the private key.</param>
		/// <param name="input">Encrypted base64 string.</param>
		/// <param name="privateKeyPassword">Optional private key password.</param>
		/// <param name="addHeaders">Wrap base64 between BEGIN DATA header and END DATA footer.</param>
		/// <returns>Encrypted text</returns>
		public static string Encrypt(string fileName, string input, string privateKeyPassword = null, bool addHeaders = false)
		{
			var cert = new X509Certificate2(fileName, privateKeyPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
			var inputBytes = Encoding.Unicode.GetBytes(input);
			var bytes = Encrypt(cert, inputBytes);
			string data = "";
			if (addHeaders)
				data += "-----BEGIN DATA-----\r\n";
			data += Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
			if (addHeaders)
				data += "\r\n-----END DATA-----";
			return data;
		}

		public static byte[] Encrypt(X509Certificate2 cert, byte[] input)
		{
			using (var publicKey = cert.GetRSAPublicKey())
			{
				var bytes = publicKey.Encrypt(input, RSAEncryptionPadding.OaepSHA256);
				return bytes;
			}
		}

		/// <summary>
		/// Decrypt encrypted base64 string. 
		/// </summary>
		/// <param name="fileName">File name which contains private key.</param>
		/// <param name="base64">Encrypted base64 string.</param>
		/// <param name="privateKeyPassword">Optional private key password.</param>
		/// <returns>Decrypted string.</returns>
		public static string Decrypt(string fileName, string base64, string privateKeyPassword = null)
		{
			var cert = new X509Certificate2(fileName, privateKeyPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
			// Strip header and footer.
			var headers = new System.Text.RegularExpressions.Regex("[-]{5,}[ A-Z]+[-]{5,}");
			base64 = headers.Replace(base64, "").Trim('\r', '\n', ' ');
			// Decrypt.
			var input = Convert.FromBase64String(base64);
			var bytes = Decrypt(cert, input);
			var ascii = Encoding.Unicode.GetString(bytes);
			return ascii;
		}

		/// <summary>
		/// Decrypt bytes string.
		/// </summary>
		/// <param name="cert">Certificate which contains private key.</param>
		/// <param name="input">Encrypted bytes.</param>
		/// <returns>Decrypted bytes.</returns>
		public static byte[] Decrypt(X509Certificate2 cert, byte[] input)
		{
			using (var privateKey = cert.GetRSAPrivateKey())
			{
				var bytes = privateKey.Decrypt(input, RSAEncryptionPadding.OaepSHA256);
				return bytes;
			}
		}

		internal class NativeMethods
		{

			[DllImport("Crypt32.dll", SetLastError = true, ExactSpelling = true)]
			internal static extern IntPtr CertCreateSelfSignCertificate(
			   IntPtr providerHandle,
			   [In] CryptoApiBlob subjectIssuerBlob,
			   int flags,
			   ref CRYPT_KEY_PROV_INFO pKeyProvInfo,
			   ref CRYPT_ALGORITHM_IDENTIFIER pSignatureAlgorithm,
			   [In] SystemTime startTime,
			   [In] SystemTime endTime,
			   IntPtr extensions);

			[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool FileTimeToSystemTime(
			   [In] ref long fileTime,
			   [Out] SystemTime systemTime);

			[StructLayout(LayoutKind.Sequential)]
			internal class CryptoApiBlob
			{
				public int DataLength;
				public IntPtr Data;

				public CryptoApiBlob(int dataLength, IntPtr data)
				{
					DataLength = dataLength;
					Data = data;
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			internal class SystemTime
			{
				public short Year;
				public short Month;
				public short DayOfWeek;
				public short Day;
				public short Hour;
				public short Minute;
				public short Second;
				public short Milliseconds;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CRYPT_KEY_PROV_INFO
			{
				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwszContainerName;
				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwszProvName;
				public uint dwProvType;
				public uint dwFlags;
				public uint cProvParam;
				public IntPtr rgProvParam;
				public uint dwKeySpec;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CRYPT_ALGORITHM_IDENTIFIER
			{
				[MarshalAs(UnmanagedType.LPStr)]
				public string pszObjId;
				public CRYPTOAPI_BLOB parameters;
			}

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			public struct CRYPTOAPI_BLOB
			{
				public uint cbData;
				public IntPtr pbData;
			}

		}

		public const string OID_RSA_SHA256RSA = "1.2.840.113549.1.1.11";
		public const string szOID_ENHANCED_KEY_USAGE = "2.5.29.37";

		public class SelfSignedCertProperties
		{
			public DateTime ValidFrom { get; set; }
			public DateTime ValidTo { get; set; }
			public X500DistinguishedName Name { get; set; }
			public int KeyBitLength { get; set; }
			public bool IsPrivateKeyExportable { get; set; }
			public SelfSignedCertProperties(string name = "self")
			{
				IsPrivateKeyExportable = true;
				var today = DateTime.Today;
				ValidFrom = today.AddDays(-1);
				ValidTo = today.AddYears(10);
				Name = new X500DistinguishedName("cn=" + name);
				KeyBitLength = 4096;
			}
		}

		public static X509Certificate2 CreateSelfSignedCertificate(SelfSignedCertProperties properties)
		{
			//GenerateSignatureKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);
			var asnName = properties.Name.RawData;
			var asnNameHandle = GCHandle.Alloc(asnName, GCHandleType.Pinned);
			var keySize = properties.KeyBitLength;
			if (keySize <= 0)
				keySize = 2048; // Min keysize
			var algoritm = OID_RSA_SHA256RSA;
			var parameters = new CspParameters()
			{
				ProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider",
				ProviderType = 24,
				KeyContainerName = Guid.NewGuid().ToString(),
				KeyNumber = (int)KeyNumber.Exchange,
				Flags = CspProviderFlags.UseMachineKeyStore
			};
			try
			{
				var signatureAlgorithm = new NativeMethods.CRYPT_ALGORITHM_IDENTIFIER
				{
					pszObjId = algoritm
				};
				signatureAlgorithm.parameters.cbData = 0;
				signatureAlgorithm.parameters.pbData = IntPtr.Zero;
				using (new RSACryptoServiceProvider(keySize, parameters))
				{
					var providerInfo = new NativeMethods.CRYPT_KEY_PROV_INFO
					{
						pwszProvName = parameters.ProviderName,
						pwszContainerName = parameters.KeyContainerName,
						dwProvType = (uint)parameters.ProviderType,
						dwFlags = 0x20, //(uint)parameters.Flags, 
						dwKeySpec = (uint)parameters.KeyNumber
					};
					IntPtr certHandle = NativeMethods.CertCreateSelfSignCertificate(
					  IntPtr.Zero,
					  new NativeMethods.CryptoApiBlob(asnName.Length, asnNameHandle.AddrOfPinnedObject()),
					  0,
					  ref providerInfo,
					  ref signatureAlgorithm,
					  ToSystemTime(properties.ValidFrom),
					  ToSystemTime(properties.ValidTo),
					  IntPtr.Zero);
					if (IntPtr.Zero == certHandle)
						ThrowExceptionIfGetLastErrorIsNotZero();
					return new X509Certificate2(certHandle);
				}
			}
			finally
			{
				// Free the unmanaged memory.
				asnNameHandle.Free();
			}
		}

		private static NativeMethods.SystemTime ToSystemTime(DateTime dateTime)
		{
			long fileTime = dateTime.ToFileTime();
			var systemTime = new NativeMethods.SystemTime();
			if (!NativeMethods.FileTimeToSystemTime(ref fileTime, systemTime))
				ThrowExceptionIfGetLastErrorIsNotZero();
			return systemTime;
		}

		internal static void ThrowExceptionIfGetLastErrorIsNotZero()
		{
			int win32ErrorCode = Marshal.GetLastWin32Error();
			if (win32ErrorCode == 0)
				return;
			if (win32ErrorCode > 0)
				win32ErrorCode = (int)((((uint)win32ErrorCode) & 0x0000FFFF) | 0x80070000U);
			Marshal.ThrowExceptionForHR(win32ErrorCode);
		}

	}
}
