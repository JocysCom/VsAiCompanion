using EnvDTE;
using EnvDTE80;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Extension
{
	public partial class SolutionHelper : ISolutionHelper
	{

		/// <summary>
		/// Switch to Visual Studio Thread.
		/// </summary>
		public async Task SwitchToMainThreadAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
		}

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
			var item = new DocItem("", solution.FullName, "Solution");
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

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetDocumentsByProject(GetProjectOfActiveDocument());
		}

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfSelectedDocument()
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

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsSelectedInExplorer()
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
						items.Add(new DocItem("", file, "Project Item"));
					}
				}
			}
			LoadData(items);
			return items;
		}

		/// <inheritdoc />
		public IList<DocItem> GetOpenDocuments()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null || dte.Documents == null)
				return new List<DocItem>();

			var items = new List<DocItem>();
			foreach (Document doc in dte.Documents)
			{
				bool hasVisibleWindow = false;
				foreach (Window win in doc.Windows)
				{
					if (win.Visible)
					{
						hasVisibleWindow = true;
						break;
					}
				}
				// If no visible window is associated, skip adding the document
				if (!hasVisibleWindow)
					continue;
				// Initialize DocItem with basic properties
				var docItem = new DocItem
				{
					FullName = doc.FullName,
					Name = doc.Name,
					Type = doc.Type,
					// Assume text until proven otherwise
					IsText = true
				};
				// Attempt to get the ProjectItem associated with the document, if available
				try
				{
					ProjectItem projectItem = doc.ProjectItem;
					if (projectItem != null)
					{
						docItem.Kind = projectItem.Kind;
						// Attempt to access language via CodeModel
						var codeModel = projectItem.FileCodeModel;
						if (codeModel != null)
							docItem.Language = codeModel.Language;
					}
				}
				catch
				{
					// Failed to retrieve ProjectItem or its properties
				}
				items.Add(docItem);
			}
			LoadData(items);
			return items;
		}

		/// <inheritdoc />
		public IList<DocItem> GetAllSolutionDocuments()
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
			foreach (var item in items)
				// Load files 1 megabytes max in size.
				totalBytesLoaded += item.LoadData(1024 * 1024);
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

		/// <inheritdoc />
		public DocItem GetActiveDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var td = GetTextDocument();
			if (td == null)
				return new DocItem("");
			var startPoint = td.StartPoint.CreateEditPoint();
			var data = startPoint.GetText(td.EndPoint);
			var di = new DocItem(data, td.Parent?.FullName, td.Type);
			di.Language = td.Language;
			return di;
		}

		/// <inheritdoc />
		public void SetActiveDocument(string contents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var td = GetTextDocument();
			if (td == null)
				return;
			var point = td.StartPoint.CreateEditPoint();
			point.ReplaceText(td.EndPoint, contents, (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
		}

		/// <inheritdoc />
		public DocItem GetSelection()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = GetTextDocument();
			if (doc == null)
				return new DocItem("");
			var data = doc.Selection?.Text;
			var di = new DocItem(data, doc.Parent?.FullName, doc.Type);
			di.Language = doc.Language;
			return di;
		}

		/// <inheritdoc />
		public void SetSelection(string contents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var document = GetTextDocument();
			if (document == null)
				return;
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

		/// <inheritdoc />
		public DocItem GetSelectedErrorDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var ei = GetSelectedError();
			if (ei == null)
				return null;
			var di = new DocItem(null, ei.File, "Error Document");
			di.Kind = di.Kind;
			return di;
		}

		public Plugins.Core.VsFunctions.ErrorItem GetSelectedError()
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
				return new Plugins.Core.VsFunctions.ErrorItem
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

		#region Exception

		/// <inheritdoc />
		public ExceptionInfo GetCurrentException()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null)
				return null;
			// If not in break mode, possibly due to an exception then return.
			if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
				return null;
			// Get the current exception
			var exceptionExpression = dte.Debugger.GetExpression("$exception", true, 100);
			var ei = new ExceptionInfo();
			if (exceptionExpression.IsValidValue)
			{
				var exception = ToDictionary(exceptionExpression);
				var members = ToDictionary(exceptionExpression.DataMembers);
				// Exception.Type
				var typeName = nameof(Type);
				if (exception.ContainsKey(typeName))
					ei.Type = $"{exception[typeName]}";
				// Exception.Message
				ei.Message = Parse(exceptionExpression.Value);
				// Exception.StackTrace
				var stackTraceName = nameof(Exception.StackTrace);
				if (members.Keys.Contains(stackTraceName))
					ei.StackTrace = Parse(members[stackTraceName]);
			}
			// Alternative way to get StackTrace.
			if (string.IsNullOrEmpty(ei.StackTrace))
				ei.StackTrace = GetStackTrace(dte.Debugger.CurrentThread.StackFrames);
			return ei;
		}

		/// <inheritdoc />
		public IList<DocItem> GetCurrentExceptionDocuments()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var details = GetCurrentException().ToString();
			var files = AppHelper.ExtractFilePaths(details);
			var items = files.Select(x => new DocItem(null, x)).ToList();
			return items;
		}

		public Dictionary<string, JsonElement> GetEnvironmentContext()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var list = new Dictionary<string, JsonElement>();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			if (dte == null)
				return list;
			var majorVersion = dte.Version; // This gets you the "17.0" part
			var fullVersion = GetVisualStudioVersion() ?? majorVersion;
			list.Add("DTE Version", JsonSerializer.SerializeToElement(fullVersion));
			return list;
		}

		public static string GetVisualStudioVersion()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			try
			{
				// Get the IVsShell service
				IVsShell shellService = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
				if (shellService == null)
					return null;
				// Get the version information
				shellService.GetProperty((int)__VSSPROPID5.VSSPROPID_ReleaseVersion, out object versionObj);
				var versionString = versionObj?.ToString() ?? "";
				var pattern = @"\d+\.\d+";
				// Using Regex to find match in the input string
				var match = Regex.Match(versionString, pattern);
				if (match.Success)
					return match.Value;
			}
			catch
			{
			}
			return null;
		}

		#endregion

		#region Formatting

		/// <inheritdoc />
		public void EditFormatDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			dte?.ExecuteCommand("Edit.FormatDocument");
		}

		/// <inheritdoc />
		public void EditFormatSelection()
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

		#region ComObject properties

		public static string Parse(string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;
			// If C# or Regex format.
			if (value.StartsWith("\"") && value.EndsWith("\""))
			{
				// Remove the surrounding quotes
				var trimmedValue = value.Substring(1, value.Length - 2);
				// Unescape any escape sequences
				var unescapedValue = System.Text.RegularExpressions.Regex.Unescape(trimmedValue);
				return unescapedValue;
			}
			// If JSON format.
			if (value.StartsWith("{\"") && value.EndsWith("\"}"))
			{
				// Remove the surrounding braces and quotes
				var trimmedValue = value.Substring(1, value.Length - 2);
				try
				{
					var parsed = JsonDocument.Parse(trimmedValue);
					var message = parsed.RootElement.GetString();
					return message;
				}
				catch (JsonException)
				{
					// If the input is not a valid JSON string, return the original value
					return value;
				}
			}
			return value;
		}

		public static Dictionary<string, string> ToDictionary(Expressions expressions)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var d = new Dictionary<string, string>();
			if (expressions == null)
				return d;
			var items = expressions.Cast<Expression>().ToArray();
			for (int m = 0; m < items.Length; m++)
			{
				var item = items[m];
				if (item.IsValidValue && !d.ContainsKey(item.Name))
					d.Add(item.Name, item.Value);
			}
			return d;
		}

		public static Dictionary<string, object> ToDictionary<T>(T o)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var d = new Dictionary<string, object>();
			if (o == null)
				return d;
			var properties = typeof(T).GetProperties();
			foreach (var p in properties)
				d.Add(p.Name, p.GetValue(o));
			return d;
		}

		public static string GetStackTrace(StackFrames stackFrames)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			StringBuilder stackTrace = new StringBuilder();
			foreach (StackFrame stackFrame in stackFrames)
			{
				string functionName = stackFrame.FunctionName;
				stackTrace.AppendLine("   at " + functionName);
			}
			return stackTrace.ToString();
		}

		#endregion

	}
}
