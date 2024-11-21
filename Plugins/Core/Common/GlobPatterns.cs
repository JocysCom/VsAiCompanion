using Microsoft.Extensions.FileSystemGlobbing;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Glob patterns helper. Patterns used by `.gitignore` files.
	/// </summary>
	public class GlobPatterns
	{
		/// <summary>
		/// Glob patterns. Used by `.gitignore` files.
		/// </summary>
		/// <param name="excludePatterns">Exclude patterns. Applies if not null.</param>
		/// <param name="includePatterns">Include patterns. Applies if not null.</param>
		/// <param name="useGitIgnoreFiles">Use git ignore files.</param>
		public GlobPatterns(string excludePatterns, string includePatterns, bool useGitIgnoreFiles)
		{
			ExcludePatterns = GetIgnoreFromText(excludePatterns);
			IncludePatterns = GetIgnoreFromText(includePatterns);
			UseGitIgnoreFiles = useGitIgnoreFiles;
			GitExcludePatterns = GetIgnoreFromText(".git/");
		}

		/// <summary>Exclude patterns</summary>
		public Matcher ExcludePatterns { get; set; }
		/// <summary>Include patterns</summary>
		public Matcher IncludePatterns { get; set; }
		/// <summary>GIT Exclude patterns</summary>
		public Matcher GitExcludePatterns { get; set; }
		/// <summary>Use `.gitignore` files.</summary>
		public bool UseGitIgnoreFiles { get; set; }

		/// <summary>
		/// Creates an <see cref="Matcher"/> object from the given text.
		/// </summary>
		/// <param name="text">The text content to parse into ignore rules.</param>
		/// <returns>
		/// An <see cref="Matcher"/> object containing the parsed rules, or <c>null</c> if no valid rules were found.
		/// </returns>
		public Matcher GetIgnoreFromText(string text)
		{

			if (text == null)
				return null;
			var matcher = new Matcher();
			var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			var count = 0;
			foreach (var line in lines)
			{
				var patternLine = line.Trim();
				if (string.IsNullOrEmpty(patternLine) || patternLine.StartsWith("#"))
					continue;
				count++;
				if (patternLine.StartsWith("!"))
				{
					patternLine = patternLine.Substring(1).TrimStart();
					matcher.AddExclude(patternLine);
				}
				else
				{
					matcher.AddInclude(patternLine);
				}
			}
			return matcher;
		}

		/// <summary>
		/// Creates an <see cref="Matcher"/> object from the specified file.
		/// </summary>
		/// <param name="path">The path to the file containing ignore rules.</param>
		/// <returns>
		/// An <see cref="Matcher"/> object containing the parsed rules, or <c>null</c> if the file does not exist or contains no valid rules.
		/// </returns>
		public Matcher GetIgnoreFromFile(string path)
		{
			var fi = new FileInfo(path);
			if (!fi.Exists)
				return null;
			var text = System.IO.File.ReadAllText(path);
			return GetIgnoreFromText(text);
		}

		/// <summary>
		/// Cache for <see cref="Matcher"/> objects to improve performance.
		/// </summary>
		/// <remarks>Cache allows for this class to work 20 times faster.</remarks>
		private ConcurrentDictionary<string, Matcher> IgnoreCache { get; } = new ConcurrentDictionary<string, Matcher>();

		/// <summary>
		/// Retrieves an <see cref="Matcher"/> object from the specified folder, optionally using a cache.
		/// </summary>
		/// <param name="path">The folder path to retrieve the ignore object from.</param>
		/// <param name="cache">If <c>true</c>, the cache will be used to retrieve the ignore object.</param>
		/// <returns>
		/// An <see cref="Matcher"/> object retrieved from the folder.
		/// </returns>
		private Matcher GetIgnoreFromDirectory(string path, bool cache = true)
		{
			var ignore = cache
				? IgnoreCache.GetOrAdd(path, x => _GetIgnoreFromDirectory(path))
				: _GetIgnoreFromDirectory(path);
			return ignore;
		}

		private Matcher _GetIgnoreFromDirectory(string path)
		{
			var ignoreFullName = Path.Combine(path, ".gitignore");
			var ignore = GetIgnoreFromFile(ignoreFullName);
			return ignore;
		}

		/// <summary>
		/// Retrieves <see cref="Matcher"/> objects by searching for <c>.gitignore</c> files in the specified folder and optionally its parent directories.
		/// </summary>
		/// <param name="path">The folder path to start the search.</param>
		/// <param name="searchParentDirectories">
		/// If <c>true</c>, searches all parent directories for <c>.gitignore</c> files; otherwise, only searches the specified folder.
		/// </param>
		/// <param name="cache">If <c>true</c>, the cache will be used to retrieve the ignore object.</param>
		/// <returns>
		/// A list of <see cref="Matcher"/> objects found in the folder and optionally its parent directories.
		/// </returns>
		public Dictionary<string, Matcher> GetGitIgnoresByDirectoryPath(string path, bool searchParentDirectories = false, bool cache = true)
		{
			var list = new Dictionary<string, Matcher>();
			var di = new DirectoryInfo(path);
			while (di != null && di.Exists)
			{
				var ignore = GetIgnoreFromDirectory(di.FullName, cache);
				if (ignore != null)
					list.Add(di.FullName, ignore);
				if (!searchParentDirectories)
					break;
				di = di.Parent;
			}
			return list;
		}

		/// <summary>
		/// Return true if file is ignored.
		/// </summary>
		public bool IsIgnored(string relativeTo, string path)
		{
			var relativePath = GetRelativePath(relativeTo, path);
			// Skip if not in the list of included.
			if (IncludePatterns != null && !IncludePatterns.Match(relativePath).HasMatches)
				return true;
			if (ExcludePatterns != null && ExcludePatterns.Match(relativePath).HasMatches)
				return true;
			if (UseGitIgnoreFiles)
			{
				// Ignore `.git` folders.
				if (GitExcludePatterns.Match(relativePath).HasMatches)
					return true;
				// Check against `.gitignore` files.
				if (IsGitIgnored(path, searchParentDirectories: true))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Returns true if file is ignored.
		/// </summary>
		/// <param name="filePath">Full path to the file.</param>
		/// <param name="searchParentDirectories">If `true`, searches all parent directories for `.gitignore` files; otherwise, only searches the file folder.</param>
		private bool IsGitIgnored(string filePath, bool searchParentDirectories)
		{
			var directoryPath = System.IO.Path.GetDirectoryName(filePath);
			var ignoreKvs = GetGitIgnoresByDirectoryPath(directoryPath, searchParentDirectories, cache: true);
			foreach (var ignoreKv in ignoreKvs)
			{
				var ignorePath = ignoreKv.Key;
				var relativeFilePath = GetRelativePath(ignorePath, filePath);
				if (ignoreKv.Value.Match(relativeFilePath).HasMatches)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Get relative path to `.gitignore`.
		/// </summary>
		/// <param name="relativeTo"></param>
		/// <param name="path"></param>
		private string GetRelativePath(string relativeTo, string path)
		{
			// Make sure relative to path have folder separator to indicate that it is folder.
			if (!relativeTo.EndsWith("\\"))
				relativeTo = relativeTo + "\\";
			var relativePath = JocysCom.ClassLibrary.IO.PathHelper.GetRelativePath(relativeTo, path, false)
				// Normalize path
				.Replace('\\', '/').TrimStart('/');
			return relativePath;
		}

	}
}
