using EnvDTE;
using EnvDTE80;
using JocysCom.VS.AiCompanion.Engine;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JocysCom.VS.AiCompanion.Extension
{
	public static partial class SolutionHelper
	{

		public static DTE2 GetCurrentService()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			return dte;
		}

		public static Solution2 GetCurrentSolution()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null)
				return null;
			if (dte.Solution.Count == 0)
				return null;
			return dte.Solution as Solution2;
		}

		public static Project GetStartupProject(Solution2 solution)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var projectName = (string)solution.Properties.Item("StartupProject").Value;
			var project = solution.Projects.Cast<Project>().FirstOrDefault(p =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return p.Name == projectName;
			});
			return project;
		}

		public static Project GetProjectOfSelectedDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null)
				return null;
			var uiHierarchy = dte.ToolWindows.SolutionExplorer;
			var selectedItems = (Array)uiHierarchy.SelectedItems;
			foreach (UIHierarchyItem selItem in selectedItems)
				if (selItem.Object is ProjectItem selectedProjectItem)
					return selectedProjectItem.ContainingProject;
			return null;
		}

		public static Project GetProjectOfActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			return dte?.ActiveDocument?.ProjectItem?.ContainingProject;
		}

		public static Project GetStartupProject()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var solution = GetCurrentSolution();
			var solutionBuild = solution?.SolutionBuild;
			var startupProjects = (object[])solutionBuild?.StartupProjects;
			if (startupProjects == null || startupProjects.Length == 0)
				return null;
			return solution?.Projects?.OfType<Project>().FirstOrDefault(p =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return p.UniqueName == (string)startupProjects[0];
			});
		}

		public static string References = "References";

		public static SolutionFolder GetReferencesFolder()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var solution = GetCurrentSolution();
			if (solution == null)
				return null;
			foreach (var project in solution.Projects)
			{
				var p = project as Project;
				if (p == null)
					continue;
				var f = p.Object as SolutionFolder;
				if (f == null)
					continue;
				if (p.Name != References)
					continue;
				return f;
			}
			return null;
		}

		public static Project GetProject(string name)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var projects = GetAllProjects();
			var project = projects.FirstOrDefault(x =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return x.Name == name;
			});
			return project;
		}

		public static List<Microsoft.Build.Evaluation.ProjectProperty> GetAllProperties()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var list = new List<Microsoft.Build.Evaluation.ProjectProperty>();
			var project = GetStartupProject();
			if (project is null)
				return list;
			var projectPath = project.FullName;
			var pc = new Microsoft.Build.Evaluation.ProjectCollection();
			var msbuildProject = pc.LoadProject(projectPath);
			list = msbuildProject.AllEvaluatedProperties
				.Where(x => !string.IsNullOrWhiteSpace(x.EvaluatedValue))
				// Include only original properties
				.Where(x => x.EvaluatedValue == x.UnevaluatedValue)
				// Only if don't start with "_"
				.Where(x => !x.Name.StartsWith("_"))
				// Not Undefined.
				.Where(x => x.EvaluatedValue != "*Undefined*")
				// Exclude multiline
				.Where(x => !x.EvaluatedValue.Contains("\r") && !x.EvaluatedValue.Contains("\n"))
				// Exclude booleans.
				.Where(x => !bool.TryParse(x.EvaluatedValue, out var value))
				// Exclude numbers.
				.Where(x => !double.TryParse(x.EvaluatedValue, out var value))
				// Exclude guids.
				.Where(x => !Guid.TryParse(x.EvaluatedValue, out var value))
				.OrderBy(x => x.Name)
				.ToList();
			return list;
		}

		public static List<PropertyItem> GetEnvironmentProperties()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetAllProperties()
				.Where(x => x.IsEnvironmentProperty)
				.Where(x => !x.IsReservedProperty)
				.Select(
				x => new PropertyItem()
				{
					Key = x.Name,
					Value = x.EvaluatedValue,
					Display = x.EvaluatedValue,
				})
				.ToList();
		}

		public static List<PropertyItem> GetReservedProperties()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetAllProperties()
				.Where(x => !x.IsEnvironmentProperty)
				.Where(x => x.IsReservedProperty)
				.Select(
				x => new PropertyItem()
				{
					Key = x.Name,
					Value = x.EvaluatedValue,
					Display = x.EvaluatedValue,
				})
				.ToList();
		}

		public static List<PropertyItem> GetOtherProperties()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetAllProperties()
				.Where(x => !x.IsEnvironmentProperty)
				.Where(x => !x.IsReservedProperty)
				.Select(
				x => new PropertyItem()
				{
					Key = x.Name,
					Value = x.EvaluatedValue,
					Display = x.EvaluatedValue,
				})
				.ToList();
		}

		public static List<Project> GetAllProjects()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var list = new List<Project>();
			var solution = GetCurrentSolution();
			if (solution == null)
				return list;
			var projects = solution.Projects;
			var item = projects.GetEnumerator();
			while (item.MoveNext())
			{
				var project = item.Current as Project;
				if (project == null)
					continue;
				if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
					list.AddRange(GetSolutionFolderProjects(project));
				else
					list.Add(project);
			}
			return list;
		}

		#region Get Documents

		public static DocItem GetDocumentsBySolution(Solution2 solution)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var item = new DocItem("")
			{
				FullName = solution.FullName,
				Name = Path.GetFileName(solution.FullName),
				Type = "Solution",
				Language = null,
			};
			LoadData(new List<DocItem> { item });
			return item;
		}

		public static List<DocItem> GetDocumentsByProject(Project project)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			RecurseProjectItems(project.ProjectItems, items);
			LoadData(items);
			return items;
		}

		public static List<DocItem> GetDocumentsOfProjectOfActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetDocumentsByProject(GetProjectOfActiveDocument());
		}

		public static List<DocItem> GetDocumentsOfProjectOfSelectedDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetDocumentsByProject(GetProjectOfSelectedDocument());
		}

		private static void RecurseProjectItems(ProjectItems projectItems, List<DocItem> items)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (ProjectItem item in projectItems)
			{
				for (short i = 1; i <= item.FileCount; i++)
				{
					var docItem = new DocItem
					{
						FullName = item.get_FileNames(i),
						Name = item.Name,
						Kind = item.Kind,
						Language = item.ContainingProject.CodeModel.Language,
						Data = null // Not populated here. Consider async file read if necessary.
					};
					items.Add(docItem);
				}
				// Recursive call if there are sub-items
				if (item.ProjectItems != null)
					RecurseProjectItems(item.ProjectItems, items);
			}
		}

		public static List<DocItem> GetDocumentsSelectedInExplorer()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null)
				return null;
			var selectedItems = dte.SelectedItems;
			foreach (SelectedItem item in selectedItems)
			{
				if (item.ProjectItem != null)
				{
					for (short i = 1; i <= item.ProjectItem.FileCount; i++)
					{
						var file = item.ProjectItem.get_FileNames(i);
						items.Add(new DocItem("")
						{
							FullName = file,
							Name = Path.GetFileName(file),
							Type = "Project Item",
							Language = null,
						});
					}
				}
			}
			LoadData(items);
			return items;
		}

		public static List<DocItem> GetAllSolutionDocuments()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			var solution = GetCurrentSolution();
			if (solution == null)
				return items;
			// Add the solution file itself
			items.Add(GetDocumentsBySolution(solution));
			// Get all projects (including solution folders)
			var projects = GetAllProjects();
			// Add all files in each project or solution folder
			foreach (var project in projects)
			{
				if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
				{
					// If this is a solution folder, it might be the solution items
					if (project.Name == "Solution Items")
						items.AddRange(GetDocumentsByProject(project));
				}
				else
				{
					// This is a regular project
					items.AddRange(GetDocumentsByProject(project));
				}
			}
			return items;
		}

		public static long LoadData(List<DocItem> items)
		{
			long totalBytesLoaded = 0;
			foreach (var doc in items)
			{
				// Don't load binary files.
				if (!doc.IsText)
					continue;
				if (string.IsNullOrEmpty(doc.FullName))
					continue;
				if (!File.Exists(doc.FullName))
					continue;
				try
				{
					doc.Data = File.ReadAllText(doc.FullName);
					totalBytesLoaded += Encoding.Unicode.GetByteCount(doc.Data);
				}
				catch
				{
					// Handle exceptions here, for example when trying to open a file 
					// that is already open in another program.
				}
			}
			return totalBytesLoaded;
		}


		#endregion

		private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var list = new List<Project>();
			for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
			{
				var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
				if (subProject == null)
					continue;
				if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder)
					list.AddRange(GetSolutionFolderProjects(subProject));
				else
					list.Add(subProject);
			}
			return list;
		}

		public static VSLangProj.VSProject GetVsProject(string name)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var project = GetProject(name);
			return project?.Object as VSLangProj.VSProject;
		}

		public static VSLangProj.Reference GetReference(VSLangProj.VSProject project, string name)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (VSLangProj.Reference reference in project.References)
				if (reference.Name == name)
					return reference;
			return null;
		}

		public static SolutionFolder GetOrCreateReferencesFolder()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var folder = GetReferencesFolder();
			if (folder != null)
				return folder;
			var solution = GetCurrentSolution();
			// Add a solution folder.
			var project = solution.AddSolutionFolder(References);
			folder = (SolutionFolder)project.Object;
			return folder;
		}

		/// <summary>
		/// Get active document.
		/// </summary>
		public static TextDocument GetTextDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = GetCurrentService();
			if (dte.ActiveDocument == null)
				return null;
			var document = dte.ActiveDocument.Object("TextDocument") as TextDocument;
			return document;
		}

		/// <summary>
		/// Get Language of selected document
		/// </summary>
		public static string GetLanguage()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetTextDocument()?.Language;
		}

		public static DocItem GetActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var mi = new DocItem("");
			var td = GetTextDocument();
			if (td == null)
				return mi;
			var startPoint = td.StartPoint.CreateEditPoint();
			var data = startPoint.GetText(td.EndPoint);
			mi.Data = data;
			mi.Type = td.Type;
			mi.Language = td.Language;
			mi.Name = td.Parent?.Name;
			return mi;
		}

		public static void SetActiveDocument(string data)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var td = GetTextDocument();
			if (td == null)
				return;
			var point = td.StartPoint.CreateEditPoint();
			point.ReplaceText(td.EndPoint, data, (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
		}

		/// <summary>
		/// Get selection contents.
		/// </summary>
		/// <returns>Selection contents.</returns>
		public static DocItem GetSelection()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var mi = new DocItem("");
			var doc = GetTextDocument();
			if (doc == null)
				return mi;
			mi.Data = doc.Selection?.Text;
			mi.Type = doc.Type;
			mi.Language = doc.Language;
			mi.Name = doc.Parent?.Name;
			return mi;
		}

		/// <summary>
		/// Set selection contents.
		/// </summary>
		/// <returns>Selection contents.</returns>
		public static void SetSelection(string contents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var document = GetTextDocument();
			var selection = document.Selection;
			selection.Delete();
			selection.Insert(contents, (int)vsInsertFlags.vsInsertFlagsInsertAtEnd);
		}

		//public static List<ErrorItem> GetErrors()
		//{
		//	ThreadHelper.ThrowIfNotOnUIThread();
		//	var dte = GetCurrentService();
		//	var errorList = dte.ToolWindows.ErrorList;
		//	var errorsCount = errorList.ErrorItems.Count;
		//	var errors = new List<ErrorItem>();
		//	for (int i = 0; i < errorsCount; i++)
		//		errors.Add(errorList.ErrorItems.Item(i + 1));
		//	return errors;
		//}


		public static Engine.ErrorItem GetSelectedError()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (!(Package.GetGlobalService(typeof(SVsErrorList)) is IVsTaskList2 tasks))
				return null;
			tasks.EnumSelectedItems(out IVsEnumTaskItems itemsEnum);
			var items = new IVsTaskItem[1];
			var fetched = new uint[1];
			itemsEnum.Next(1, items, fetched);
			if (fetched[0] > 0)
			{
				IVsTaskItem task = items[0];
				task.Document(out string file);
				task.Line(out int line);
				task.Column(out int column);
				task.get_Text(out string description);
				var category = new VSTASKCATEGORY[1];
				task.Category(category);
				return new Engine.ErrorItem
				{
					File = file,
					Line = line,
					Column = column,
					Description = description,
					Category = category?.Select(x => x.ToString()).ToArray()
				};
			}
			return null;
		}

		#region Formatting

		public static void EditFormatDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			dte?.ExecuteCommand("Edit.FormatDocument");
		}

		public static void EditFormatSelection()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			dte?.ExecuteCommand("Edit.FormatSelection");
		}

		public static void EditSelect(
			int startLine, int endLine,
			int? startOffset = null, int? endOffset = null)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			var doc = dte?.ActiveDocument.Object("TextDocument") as TextDocument;
			if (doc == null)
				return;
			startOffset = startOffset ?? 1;
			var lastLine = doc.EndPoint.Line;
			if (startLine > lastLine || endLine > lastLine)
				return;
			var startPoint = doc.CreateEditPoint();
			startPoint.MoveToLineAndOffset(startLine, startOffset.Value);
			if (startPoint.Line != startLine)
				return;
			var endPoint = doc.CreateEditPoint();
			endPoint.MoveToLineAndOffset(endLine, endPoint.LineLength + 1);
			endOffset = endOffset ?? endPoint.LineCharOffset;
			if (endLine == lastLine && endOffset > endPoint.LineCharOffset)
				return;
			doc.Selection.MoveToLineAndOffset(startLine, startOffset.Value, false);
			doc.Selection.MoveToLineAndOffset(endLine, endOffset.Value, true);
		}

		#endregion

	}
}
