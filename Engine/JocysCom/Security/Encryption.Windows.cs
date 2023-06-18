using System.Security.Cryptography;

namespace JocysCom.ClassLibrary.Security
{
	/// <summary>
	/// Summary description for Encryption
	/// </summary>
	public partial class Encryption
	{

		public static string Encrypt(string decryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var data = System.Text.Encoding.Unicode.GetBytes(decryptedText);
			var cypher = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
			return System.Convert.ToBase64String(cypher);
		}

		public static string Decrypt(string encryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var data = System.Convert.FromBase64String(encryptedText);
			var decrypted = ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser);
			return System.Text.Encoding.Unicode.GetString(decrypted);
		}

	}
}
