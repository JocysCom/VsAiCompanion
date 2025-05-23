using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace JocysCom.ClassLibrary.Configuration
{
	public partial class AssemblyInfo
	{
		public string AppUserData
		{
			get => _AppUserData ?? Entry.GetAppDataPath(true);
			set => _AppUserData = value;
		}
		string _AppUserData;

		public string AppCommonData
		{
			get => _AppCommonData ?? Entry.GetAppDataPath(false);
			set => _AppCommonData = value;
		}
		string _AppCommonData;

		public string ModuleFileName
		{
			get => _ModuleFileName ?? System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
			set => _ModuleFileName = value;
		}
		string _ModuleFileName;

		public string ModuleDirectory
			=> string.IsNullOrEmpty(ModuleFileName) ? null : Path.GetDirectoryName(ModuleFileName);

		public string ModuleBaseName
			=> string.IsNullOrEmpty(ModuleFileName) ? null : Path.GetFileNameWithoutExtension(ModuleFileName);

		public string ModuleBasePath
			=> string.IsNullOrEmpty(ModuleFileName) ? null : Path.Combine(ModuleDirectory, ModuleBaseName);


		#region Parametrize and Expand

		public static string ExpandPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return path;
			// Variables are quoted with '%' (percent) sign.
			path = Environment.ExpandEnvironmentVariables(path);
			// Variables are quoted with '{' and '}' sign.
			path = JocysCom.ClassLibrary.Text.Helper.Replace(path, Entry, false);
			return path;
		}

		public static string ParameterizePath(string path, bool useEnvironmentVariables = false)
		{
			if (string.IsNullOrEmpty(path))
				return path;
			// Variables are quoted with '{' and '}' sign
			path = JocysCom.ClassLibrary.Text.Helper.Replace(Entry, path, false);
			if (useEnvironmentVariables)
				path = ReplaceWithEnvironmentVariables(path);
			return path;
		}

		public static string ReplaceWithEnvironmentVariables(string input)
		{
			var invalidPathChars = Path.GetInvalidPathChars();
			var kvs = Environment.GetEnvironmentVariables()
				.Cast<DictionaryEntry>()
				.ToDictionary(x => x.Key, x => $"{x.Value}")
				.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
				.Where(kv => kv.Value.IndexOfAny(invalidPathChars) == -1)
				.Where(kv => Path.IsPathRooted(kv.Value))
				.OrderByDescending(kv => kv.Value.Length)
				.ToList();
			foreach (var kv in kvs)
				if (input.Contains(kv.Value))
					input = input.Replace(kv.Value, $"%{kv.Key}%");
			return input;
		}



		//public static string GetExpandedPath(string path)
		//{
		//	path = ExpandPath(path);
		//	path = IO.PathHelper.ConvertFromSpecialFoldersPattern(path, "{", "}");
		//	return path;
		//}

		//public static string GetParametrizedPath(string path)
		//{
		//	path = IO.PathHelper.ConvertToSpecialFoldersPattern(path, "{", "}");
		//	path = ParameterizePath(path);
		//	return path;
		//}

		#endregion

	}
}
