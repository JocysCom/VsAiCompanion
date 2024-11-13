using EnvDTE;
using EnvDTE80;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Extension
{
	public partial class SolutionHelper : ISolutionHelper
	{

		FileHelper fileHelper = new FileHelper();

		/// <summary>
		/// Switch to Visual Studio Thread.
		/// </summary>
		public async Task SwitchToMainThreadAsync(CancellationToken cancellationToken)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
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

		public List<KeyValuePair<string, string>> GetProperties()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var properties = GetAllProperties();
			var list = new List<KeyValuePair<string, string>>();
			foreach (var prop in properties)
				list.Add(new KeyValuePair<string, string>(prop.Name, prop.EvaluatedValue));
			return list;
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

		public static List<DocItem> GetDocumentsByProject(Project project, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			RecurseProjectItems(project.ProjectItems, items);
			if (includeContents)
				LoadData(items);
			return items;
		}

		/// <inheritdoc />
		public DocItem GetSolution(bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = GetCurrentService();
			if (dte != null && dte.Solution != null)
			{
				var solution = dte.Solution as Solution2;
				var solutionFilePath = solution?.FullName;
				if (!string.IsNullOrWhiteSpace(solutionFilePath))
				{
					var docItem = Convert(solution, includeContents);
					return docItem;
				}
			}
			return null;
		}

		/// <inheritdoc />
		public IList<DocItem> GetSolutionProjects(string fileFullName, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			var solution = GetCurrentSolution();
			if (solution == null)
				return items;
			foreach (Project project in GetAllProjects())
			{
				if (!string.IsNullOrWhiteSpace(fileFullName) && !project.FullName.Equals(fileFullName, StringComparison.OrdinalIgnoreCase))
					continue;
				var docItem = Convert(project, includeContents);
				items.Add(docItem);
			}
			return items;
		}

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfCurrentDocument(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetDocumentsByProject(GetProjectOfActiveDocument(), includeContent);
		}

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfSelectedDocument(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return GetDocumentsByProject(GetProjectOfSelectedDocument(), includeContent);
		}

		private static void RecurseProjectItems(ProjectItems projectItems, List<DocItem> items)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (ProjectItem item in projectItems)
			{
				for (short i = 1; i <= item.FileCount; i++)
				{
					var docItem = Convert(item, item.FileNames[i], false);
					items.Add(docItem);
				}
				// Recursive call if there are sub-items
				if (item.ProjectItems != null)
					RecurseProjectItems(item.ProjectItems, items);
			}
		}

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsSelectedInExplorer(bool includeContent)
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
						var fullName = item.ProjectItem.get_FileNames(i);
						var docItem = Convert(item.ProjectItem, fullName, includeContent);
						items.Add(docItem);
					}
				}
			}
			return items;
		}

		/// <inheritdoc />
		public IList<DocItem> GetAllSolutionDocuments(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			var solution = GetCurrentSolution();
			if (solution == null)
				return items;
			// Add the solution file itself
			var solutionDocItem = GetSolution(includeContent);
			items.Add(solutionDocItem);
			// Get all projects (including solution folders)
			var projects = GetAllProjects();
			// Add all files in each project or solution folder
			foreach (var project in projects)
			{
				if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
				{
					// If this is a solution folder, it might be the solution items
					if (project.Name == "Solution Items")
						items.AddRange(GetDocumentsByProject(project, includeContent));
				}
				else
				{
					// This is a regular project
					items.AddRange(GetDocumentsByProject(project, includeContent));
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

		#region Context: Open Documents

		/// <inheritdoc />
		public IList<DocItem> GetOpenDocuments(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return _GetOpenDocuments(includeContent);
		}

		private IList<DocItem> _GetOpenDocuments(bool includeContent)
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
				var docItem = Convert(doc, false);
				items.Add(docItem);
			}
			var activeDocs = _GetCurrentDocuments(false);
			AddContextType(activeDocs, items, ContextType.ActiveDocument);
			if (includeContent)
				LoadData(items);
			return items;
		}

		#endregion

		#region Context: Current Document 

		/// <inheritdoc />
		public DocItem GetCurrentDocument(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return _GetCurrentDocuments(includeContent).FirstOrDefault() ?? new DocItem("");
		}

		/// <inheritdoc />
		public bool OpenDocument(string fileFullName)
		{
			return _DocumentAction(fileFullName, (doc) =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				doc.Activate();
				return true;
			});
		}

		/// <inheritdoc />
		public bool CloseDocument(string fileFullName, bool save)
		{
			return _DocumentAction(fileFullName, (doc) =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				var param = save
					? vsSaveChanges.vsSaveChangesYes
					: vsSaveChanges.vsSaveChangesNo;
				doc.Close(param);
				return true;
			});
		}

		/// <inheritdoc />
		public bool UndoDocument(string fileFullName)
		{
			return _DocumentAction(fileFullName, (doc) =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				doc.Undo();
				return true;
			});
		}

		/// <inheritdoc />
		public bool SaveDocument(string fileFullName, string newFileFullName)
		{
			return _DocumentAction(fileFullName, (doc) =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				vsSaveStatus status;
				if (string.IsNullOrEmpty(newFileFullName))
					status = doc.Save();
				else
					status = doc.Save(newFileFullName);
				return status == vsSaveStatus.vsSaveSucceeded;
			});
		}

		private bool _DocumentAction(string fileFullName, Func<Document, bool> action)
		{
			// Switch to the main thread as required by most DTE operations.
			ThreadHelper.ThrowIfNotOnUIThread();
			try
			{
				// Get the DTE2 service.
				var dte = GetCurrentService();
				// Iterate through the open documents to find a match.
				foreach (Document doc in dte.Documents)
					if (doc.FullName.Equals(fileFullName, StringComparison.OrdinalIgnoreCase))
						return action.Invoke(doc);
			}
			catch (Exception ex)
			{
				// Log or handle exceptions as necessary.
				// This catches general exceptions for simplification.
				// Consider more specific exception handling in a real environment.
				System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
			}
			return false;
		}

		/// <inheritdoc />
		public string ModifyCurrentDocument(long startLine, long deleteLines, string insertContents = null)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			try
			{
				var docItem = GetCurrentDocument(true);
				var contents = docItem.ContentData;
				contents = fileHelper.ModifyText(contents, startLine, deleteLines, insertContents);
				return SetCurrentDocumentContents(contents) ? "OK" : "Failed";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}


		/// <inheritdoc />
		public bool SetCurrentDocumentContents(string contents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var td = GetTextDocument();
			if (td == null)
				return false;
			var point = td.StartPoint.CreateEditPoint();
			point.ReplaceText(td.EndPoint, contents, (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
			return true;
		}

		private IList<DocItem> _GetCurrentDocuments(bool includeContent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = new List<DocItem>();
			var td = GetTextDocument();
			if (td == null)
				return items;
			var docItem = Convert(td, includeContent);
			docItem.ContextType = ContextType.ActiveDocument | ContextType.OpenDocuments;
			items.Add(docItem);
			return items;
		}

		#endregion

		#region Context: Selection

		/// <inheritdoc />
		public DocItem GetSelection()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = GetTextDocument();
			if (doc == null)
				return new DocItem("");
			var docItem = Convert(doc, false);
			docItem.ContentData = doc.Selection?.Text;
			docItem.ContextType = ContextType.Selection;
			return docItem;
		}

		/// <inheritdoc />
		public bool SetSelection(string contents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var document = GetTextDocument();
			if (document == null)
				return false;
			var selection = document.Selection;
			selection.Delete();
			selection.Insert(contents, (int)vsInsertFlags.vsInsertFlagsInsertAtEnd);
			return true;
		}

		/// <inheritdoc />
		public string ModifySelection(long startLine, long deleteLines, string insertContents = null)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			try
			{
				var docItem = GetSelection();
				var contents = docItem.ContentData;
				contents = fileHelper.ModifyText(contents, startLine, deleteLines, insertContents);
				return SetSelection(contents) ? "OK" : "Failed";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		#endregion

		/// <inheritdoc />
		public IList<Plugins.Core.VsFunctions.ErrorItem> GetErrors(
			ErrorLevel? errorLevel = null,
			string project = null,
			string fileFullName = null,
			bool includeDocItem = false,
			bool includeDocItemContents = false
		)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			// Get the DTE2 service.
			var dte = GetCurrentService();
			var errors = new List<Plugins.Core.VsFunctions.ErrorItem>();
			ErrorList errorList = dte.ToolWindows.ErrorList;
			ErrorItems errorItems = errorList.ErrorItems;
			for (int i = 1; i <= errorItems.Count; i++)
			{
				var errorItem = errorItems.Item(i);
				var errLevel = (ErrorLevel)(int)errorItem.ErrorLevel;

				if (errorLevel != null && errorLevel.Value != errLevel)
					continue;
				if (!string.IsNullOrEmpty(project) && errorItem.Project != project)
					continue;
				if (!string.IsNullOrEmpty(fileFullName) && errorItem.FileName != fileFullName)
					continue;
				var ei = new Plugins.Core.VsFunctions.ErrorItem
				{
					ErrorLevel = errLevel,
					Project = errorItem.Project,
					File = errorItem.FileName,
					Line = errorItem.Line,
					Column = errorItem.Column,
					Description = errorItem.Description,
				};
				if (includeDocItem || includeDocItemContents)
				{
					var di = new DocItem(null, ei.File, "Error Document");
					di.Kind = di.Kind; // May need actual kind retrieval logic.
					if (includeDocItemContents)
						di.LoadData();
					ei.DocumentFile = di;
				}
				errors.Add(ei);
			}
			return errors;
		}

		/// <inheritdoc />
		public IList<Plugins.Core.VsFunctions.ErrorItem> GetSelectedErrors(bool includeDocItem, bool includeDocItemContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (!(Package.GetGlobalService(typeof(SVsErrorList)) is IVsTaskList2 tasks))
				return null;
			var errors = new List<Plugins.Core.VsFunctions.ErrorItem>();
			IVsEnumTaskItems itemsEnum;
			// Filter based on selection
			tasks.EnumSelectedItems(out itemsEnum);
			IVsTaskItem[] items = new IVsTaskItem[1];
			uint[] fetched = new uint[1];

			while (itemsEnum.Next(1, items, fetched) == 0 && fetched[0] > 0)
			{
				IVsTaskItem task = items[0];
				task.Document(out string file);
				task.Line(out int line);
				task.Column(out int column);
				task.get_Text(out string description);
				var category = new VSTASKCATEGORY[1];
				task.Category(category);
				var ei = new Plugins.Core.VsFunctions.ErrorItem
				{
					File = file,
					Line = line,
					Column = column,
					Description = description,
					Category = category?.Select(x => x.ToString()).ToArray()
				};
				if (includeDocItem || includeDocItemContents)
				{
					var di = new DocItem(null, ei.File, "Error Document");
					di.Kind = di.Kind; // May need actual kind retrieval logic.
					if (includeDocItemContents)
						di.LoadData();
					ei.DocumentFile = di;
				}
				errors.Add(ei);
			}
			return errors;
		}

		#region Exception

		/// <inheritdoc />
		public ExceptionInfo GetCurrentException(bool includeDocItem, bool includeDocItemContents)
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
			if (includeDocItem || includeDocItemContents)
			{
				var files = AppHelper.ExtractFilePaths(ei.ToString());
				var items = files.Select(x => new DocItem(null, x)).ToList();
				if (includeDocItemContents)
				{
					foreach (var item in items)
						item.LoadData();
				}
			}
			return ei;
		}

		#endregion

		#region Other Functions

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

		private void AddContextType(IList<DocItem> source, IList<DocItem> target, ContextType contextType)
		{
			var sourceNames = source
				.Select(x => x.FullName)
				.Where(x => !string.IsNullOrEmpty(x))
				.ToList();
			foreach (var item in target)
				if (sourceNames.Contains(item.FullName))
					item.ContextType |= contextType;
		}

		#endregion

		#region Formatting

		/// <inheritdoc />
		public bool EditFormatDocument()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			dte?.ExecuteCommand("Edit.FormatDocument");
			return true;
		}

		/// <inheritdoc />
		public bool EditFormatSelection()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
			dte?.ExecuteCommand("Edit.FormatSelection");
			return true;
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
			var items = expressions.Cast<EnvDTE.Expression>().ToArray();
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

		#region Build Actions

		/// <inheritdoc />
		public string BuildSolutionProject(string fileFullName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			DTE2 dte = GetCurrentService();
			if (dte == null)
				return "Unable to retrieve the DTE service.";
			if (dte.Solution == null || dte.Solution.IsOpen == false)
				return "No solution is currently open.";
			// Find the project by its full name
			Project projectToBuild = null;
			foreach (Project project in dte.Solution.Projects)
			{
				if (project.FullName == fileFullName)
				{
					projectToBuild = project;
					break;
				}
			}

			if (projectToBuild == null)
				return $"Project with the name {fileFullName} could not be found.";
			var solutionBuild = dte.Solution.SolutionBuild as SolutionBuild2;
			if (solutionBuild == null)
				return "Unable to access the solution build.";
			try
			{
				// Build the specified project
				solutionBuild.BuildProject(solutionBuild.ActiveConfiguration.Name, projectToBuild.UniqueName, true);
				if (solutionBuild.LastBuildInfo == 0) // LastBuildInfo == 0 indicates a successful build
					return $"{projectToBuild.Name} build succeeded.";
				return $"{projectToBuild.Name} build failed.";
			}
			catch (Exception ex)
			{
				return $"Failed to build {projectToBuild.Name}: {ex.Message}";
			}
		}

		#endregion

		#region Convert to DocItem

		private static DocItem Convert(Solution2 o, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var solutionFilePath = o.FullName;
			var docItem = new DocItem
			{
				Name = Path.GetFileName(solutionFilePath),
				FullName = solutionFilePath,
				Kind = nameof(Solution),
				Language = "", // The language is not applicable for solution files.
			};
			if (includeContents)
				LoadData(new List<DocItem> { docItem });
			return docItem;
		}

		private static DocItem Convert(Project o, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var docItem = new DocItem
			{
				Name = o.Name,
				FullName = o.FullName,
				Kind = nameof(Project),
				Language = o.CodeModel?.Language,
			};
			if (includeContents)
				LoadData(new List<DocItem> { docItem });
			return docItem;
		}

		private static DocItem Convert(ProjectItem o, string fileFullName, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var docItem = new DocItem
			{
				FullName = fileFullName,
				Name = o.Name,
				ProjectName = o.ContainingProject?.Name,
				Kind = o.Kind,
				Language = o.ContainingProject.CodeModel.Language,
				IsSaved = o.Saved,
			};
			docItem.LoadFileInfo();
			if (includeContents)
				LoadData(new List<DocItem> { docItem });
			return docItem;
		}

		private static DocItem Convert(Document o, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var docItem = new DocItem
			{
				FullName = o.FullName,
				Name = o.Name,
				Kind = o.Kind,
				Language = o.Language,
				IsSaved = o.Saved,
				DocumentType = o.Type,
				// Assume text until proven otherwise
				IsText = true
			};
			// Attempt to get the ProjectItem associated with the document, if available
			try
			{
				ProjectItem projectItem = o.ProjectItem;
				if (projectItem != null)
				{
					docItem.Kind = projectItem.Kind;
					docItem.ProjectName = projectItem.ContainingProject?.Name;
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
			docItem.LoadFileInfo();
			if (includeContents)
				LoadData(new List<DocItem> { docItem });
			return docItem;
		}

		private static DocItem Convert(TextDocument td, bool includeContents)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var docItem = td.Parent == null
				? new DocItem("", "", td.Type)
				: Convert(td.Parent, false);
			docItem.Language = td.Language;
			// Include current content from the screen.
			if (includeContents)
			{
				var startPoint = td.StartPoint.CreateEditPoint();
				var data = startPoint.GetText(td.EndPoint);
				docItem.ContentData = data;
			}
			return docItem;
		}

		public void InvokeOleCommand(IntPtr hwnd, uint commandId)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			IntPtr ptr = Marshal.AllocCoTaskMem(IntPtr.Size);
			Marshal.WriteIntPtr(ptr, IntPtr.Zero);
			var cmdTarget = (IOleCommandTarget)Marshal.GetTypedObjectForIUnknown(hwnd, typeof(IOleCommandTarget));
			cmdTarget.Exec(Guid.Empty, commandId, 0, IntPtr.Zero, ptr);
			Marshal.Release(ptr);
		}

		#endregion

	}
}
