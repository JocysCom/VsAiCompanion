﻿using System.Text;

namespace JocysCom.VS.AiCompanion.ClientGenerator
{
	public static class WriteHelper
	{

		/// <summary>
		/// Returns true if the file does not exist, the file size is different, or the file contents are different.
		/// </summary>
		/// <param name="path">File path.</param>
		/// <param name="bytes">Contents to compare with the contents of the file.</param>
		public static bool IsDifferent(string path, byte[] bytes)
		{
			if (bytes is null)
				throw new ArgumentNullException(nameof(bytes));
			var fileInfo = new FileInfo(path);
			// If the file does not exist or the size is different, then it is considered different.
			if (!fileInfo.Exists || fileInfo.Length != bytes.Length)
				return true;
			// Compare checksums.
			using (var algorithm = System.Security.Cryptography.SHA256.Create())
			{
				var byteHash = algorithm.ComputeHash(bytes);
				var fileBytes = File.ReadAllBytes(fileInfo.FullName);
				var fileHash = algorithm.ComputeHash(fileBytes);
				var isDifferent = !byteHash.SequenceEqual(fileHash);
				return isDifferent;
			}
		}

		/// <summary>
		/// Writes the file contents if they are different from the existing file and returns a boolean indicating if the file was written.
		/// </summary>
		/// <param name="path">File path.</param>
		/// <param name="bytes">File contents to be written.</param>
		public static bool WriteIfDifferent(string path, byte[] bytes, bool createDir = false)
		{
			var dirPath = System.IO.Path.GetDirectoryName(path);
			if (dirPath != null && !System.IO.Directory.Exists(dirPath))
				System.IO.Directory.CreateDirectory(dirPath);
			var isDifferent = IsDifferent(path, bytes);
			if (isDifferent)
			{
				File.WriteAllBytes(path, bytes);
				Console.WriteLine($"Saved: {path}");
			}
			else
			{
				Console.WriteLine($"Same:  {path}");
			}
			return isDifferent;
		}

		/// <summary>
		/// Write generated C# content to the file.
		/// </summary>
		/// <param name="path">Path to the C# file.</param>
		/// <param name="contents">C# file contents to write.</param>
		public static void SaveToFile(string path, string contents, bool createDir = false)
		{
			var dirPath = System.IO.Path.GetDirectoryName(path);
			if (dirPath != null && !System.IO.Directory.Exists(dirPath))
				System.IO.Directory.CreateDirectory(dirPath);
			var bytes = Encoding.UTF8.GetBytes(contents);
			WriteIfDifferent(path, bytes);
		}

	}
}
