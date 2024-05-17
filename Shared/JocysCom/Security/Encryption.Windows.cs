using System.Security.Cryptography;

namespace JocysCom.ClassLibrary.Security
{
	/// <summary>
	/// Summary description for Encryption
	/// </summary>
	public partial class Encryption
	{

		public static byte[] Encrypt(byte[] decryptedData, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var encryptedData = ProtectedData.Protect(decryptedData, entropy, DataProtectionScope.CurrentUser);
			return encryptedData;
		}

		public static byte[] Decrypt(byte[] encryptedData, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
			return decryptedData;
		}

		public static string Encrypt(string decryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var decryptedData = System.Text.Encoding.Unicode.GetBytes(decryptedText);
			var encryptedData = Encrypt(decryptedData, salt, scope);
			return System.Convert.ToBase64String(encryptedData);
		}

		public static string Decrypt(string encryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var encryptedData = System.Convert.FromBase64String(encryptedText);
			var decryptedData = Decrypt(encryptedData, salt, scope);
			return System.Text.Encoding.Unicode.GetString(decryptedData);
		}

	}
}
