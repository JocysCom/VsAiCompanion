using System.Security.Cryptography;

namespace JocysCom.ClassLibrary.Security
{
	/// <summary>
	/// Provides methods for encrypting and decrypting data.
	/// </summary>
	public partial class Encryption
	{

		/// <summary>
		/// Encrypts the specified byte array using optional salt.
		/// </summary>
		/// <param name="decryptedData">The data to encrypt.</param>
		/// <param name="salt">The optional salt value. Default is "Salt Is Optional".</param>
		/// <param name="scope">The scope for encryption. Default is DataProtectionScope.CurrentUser.</param>
		/// <returns>A byte array representing the encrypted data.</returns>
		public static byte[] Encrypt(byte[] decryptedData, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var encryptedData = System.Security.Cryptography.ProtectedData.Protect(decryptedData, entropy, DataProtectionScope.CurrentUser);
			return encryptedData;
		}

		/// <summary>
		/// Decrypts the specified encrypted byte array using optional salt.
		/// </summary>
		/// <param name="encryptedData">The encrypted data to decrypt.</param>
		/// <param name="salt">The optional salt value. Default is "Salt Is Optional".</param>
		/// <param name="scope">The scope for decryption. Default is DataProtectionScope.CurrentUser.</param>
		/// <returns>A byte array representing the decrypted data.</returns>
		public static byte[] Decrypt(byte[] encryptedData, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var entropy = System.Text.Encoding.Unicode.GetBytes(salt ?? "Salt Is Optional");
			var decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
			return decryptedData;
		}

		/// <summary>
		/// Encrypts the specified string using optional salt.
		/// </summary>
		/// <param name="decryptedText">The text to encrypt.</param>
		/// <param name="salt">The optional salt value. Default is "Salt Is Optional".</param>
		/// <param name="scope">The scope for encryption. Default is DataProtectionScope.CurrentUser.</param>
		/// <returns>A Base64-encoded string representing the encrypted data.</returns>
		public static string Encrypt(string decryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var decryptedData = System.Text.Encoding.Unicode.GetBytes(decryptedText);
			var encryptedData = Encrypt(decryptedData, salt, scope);
			return System.Convert.ToBase64String(encryptedData);
		}

		/// <summary>
		/// Decrypts the specified Base64-encoded encrypted string using optional salt.
		/// </summary>
		/// <param name="encryptedText">The encrypted text to decrypt.</param>
		/// <param name="salt">The optional salt value. Default is "Salt Is Optional".</param>
		/// <param name="scope">The scope for decryption. Default is DataProtectionScope.CurrentUser.</param>
		/// <returns>A string representing the decrypted data.</returns>
		public static string Decrypt(string encryptedText, string salt = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
		{
			var encryptedData = System.Convert.FromBase64String(encryptedText);
			var decryptedData = Decrypt(encryptedData, salt, scope);
			return System.Text.Encoding.Unicode.GetString(decryptedData);
		}

	}
}
