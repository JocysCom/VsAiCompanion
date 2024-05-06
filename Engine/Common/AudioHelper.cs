using System.Linq;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AudioHelper
	{

		/// <summary>
		/// Get unique file name to save by content.
		/// </summary>
		public static string GetUniqueFilePath(
			string app,
			string groupName, string voiceName, string gender, string effect,
			string text)
		{
			if (!string.IsNullOrEmpty(groupName))
				groupName = JocysCom.ClassLibrary.Text.Filters.GetKey(groupName, false);
			string fileName;
			var encoding = System.Text.Encoding.UTF8;
			var charPath = JocysCom.ClassLibrary.Text.Filters.GetKey(string.Format("{0}_{1}_{2}", voiceName, gender, effect ?? ""), false);
			// Generalize text if needed.
			fileName = JocysCom.ClassLibrary.Text.Filters.GetKey(text, false);
			// If file name will be short then...
			if (fileName.Length >= 64)
			{
				var bytes = encoding.GetBytes(fileName);
				var algorithm = System.Security.Cryptography.SHA256.Create();
				var hash = string.Join("", algorithm.ComputeHash(bytes).Take(8).Select(x => x.ToString("X2")));
				// Return trimmed name with hash.
				fileName = string.Format("{0}_{1}", fileName.Substring(0, 64), hash);
			}
			var names = new string[] { app, groupName, charPath, fileName }.Where(x => !string.IsNullOrEmpty(x));
			var relatvePath = string.Join("\\", names);
			return relatvePath;
		}

	}
}
