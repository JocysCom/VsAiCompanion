using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.IO;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	public partial class Basic
	{
		#region File Operations

		/// <summary>
		/// Read plain text content from files and documents.
		/// Supported source formats: .docx, .xlsx, .xls, .pdf
		/// Supported target formats: .txt
		/// </summary>
		/// <param name="sourcePath">File to read from.</param>
		/// <param name="targetPath">File to write to.</param>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<bool> ConvertFile(string sourcePath, string targetPath)
		{
			var list = new List<OperationResult<bool>>();
			try
			{
				var ext = Path.GetExtension(targetPath).ToLower();
				switch (ext)
				{
					case ".txt":
						var txtReadResult = fileHelper.ReadFileAsPlainText(sourcePath);
						if (!txtReadResult.Success)
							return txtReadResult.ToResult(false);
						var text = txtReadResult.Data;
						var txtWriteResult = WriteFileText(targetPath, text);
						return txtReadResult.ToResult(false);
					default:
						return new OperationResult<bool>(new Exception($"Unknown target extension: '{ext}'"));
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Read plain text content from files and documents. Supported document formats include: .docx, .xlsx, .xls, .pdf.
		/// Supports reading multiple files at once.
		/// </summary>
		/// <param name="paths">List of files to read from.</param>
		[RiskLevel(RiskLevel.Medium)]
		public List<OperationResult<string>> ReadFilesAsPlainText(string[] paths)
		{
			var list = new List<OperationResult<string>>();
			foreach (var path in paths)
			{
				var item = fileHelper.ReadFileAsPlainText(path);
				list.Add(item);
			}
			return list;
		}

		/// <summary>
		/// Read information and contents of files.
		/// </summary>
		/// <param name="paths">The list of files or folders to read from. If a path points to a folder, 
		/// all files within that folder will be processed.</param>
		/// <param name="includeContents">`true` to include full content and metadata (size, last write, creation time); 
		/// `false` for metadata only.</param>
		/// <param name="recursive">When true, process subdirectories recursively when a folder is provided.</param>
		/// <returns>A list of DocItem objects containing the requested file information.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public static List<DocItem> ReadFiles(string[] paths, bool includeContents, bool recursive = false)
		{
			var list = new List<DocItem>();
			var filePaths = FileHelper.ExpandPathsToFiles(paths, recursive);
			foreach (var filePath in filePaths)
			{
				var di = new DocItem(null, filePath);
				di.LoadFileInfo();
				if (includeContents)
					di.LoadData();
				list.Add(di);
			}
			return list;
		}


		/// <summary>
		/// Write file text content on user computer.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="text">The string to write to the file.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		public static OperationResult<bool> WriteFileText(string path, string text)
		{
			try
			{
				var fi = new FileInfo(path);
				if (!fi.Directory.Exists)
					fi.Directory.Create();
				System.IO.File.WriteAllText(path, text);
				return new OperationResult<bool>(true, 0, $"File Created: '{path}'");
			}
			catch (System.Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Write file byte content on user computer.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="base64">The bytes represented as base64 to write to the file.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		public static OperationResult<bool> WriteFileBytes(string path, string base64)
		{
			try
			{
				var bytes = System.Convert.FromBase64String(base64);
				var fi = new FileInfo(path);
				if (!fi.Directory.Exists)
					fi.Directory.Create();
				System.IO.File.WriteAllBytes(path, bytes);
				return new OperationResult<bool>(true);
			}
			catch (System.Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		#endregion

		#region IDiffHelper

		DiffHelper diffHelper = new DiffHelper();

		/// <inheritdoc/>
		public string CompareFilesAndReturnChanges(string originalFileFullName, string modifiedFileFullName)
			=> diffHelper.CompareFilesAndReturnChanges(originalFileFullName, modifiedFileFullName);

		/// <inheritdoc/>
		public string CompareContentsAndReturnChanges(string originalText, string modifiedText)
			=> diffHelper.CompareContentsAndReturnChanges(originalText, modifiedText);

		/// <inheritdoc/>
		public string ModifyFile(string fullFileName, string unifiedDiff)
			=> diffHelper.ModifyFile(fullFileName, unifiedDiff);


		/// <inheritdoc/>
		public string ModifyContents(string contents, string unifiedDiff)
			=> diffHelper.ModifyContents(contents, unifiedDiff);

		/*

		/// <inheritdoc/>
		public string PatchText(string contents, TextPatch[] textPatches)
			=> diffHelper.PatchText(contents, textPatches);

		*/

		#endregion

		#region IFileHelper

		FileHelper fileHelper = new FileHelper();

		/// <inheritdoc/>
		public string ModifyTextFile(string path, long startLine, long deleteLines, string insertContents = null)
			=> fileHelper.ModifyTextFile(path, startLine, deleteLines, insertContents);

		/// <inheritdoc/>
		public string ReadTextFile(string path, long offset = 0, long length = long.MaxValue)
			=> fileHelper.ReadTextFile(path, offset, length);

		/// <inheritdoc/>
		public string ReadTextFileLines(string path, long line = 1, long count = long.MaxValue)
			=> fileHelper.ReadTextFileLines(path, line, count);

		#endregion

	}
}
