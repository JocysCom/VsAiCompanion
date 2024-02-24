using System.Collections.Generic;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Provides functionalities for interacting with Documents and solutions in Visual Studio,
	/// handling errors, and formatting code.
	/// </summary>
	public interface ISolutionHelper
	{

		/// <summary>
		/// Retrieves solution document.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>Soulution document.</returns>
		[RiskLevel(RiskLevel.Low)]
		DocItem GetSolution(bool includeContents);

		/// <summary>
		/// Retrieves projects of the solution.
		/// </summary>
		/// <param name="fullName">If specified, then only the specified project document will be retrieved.</param>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>Project documents.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetSolutionProjects(string fullName, bool includeContents);

		/// <summary>
		/// Retrieves all Documents throughout the entire solution.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>A collection of all Documents within the solution.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetAllSolutionDocuments(bool includeContents);

		/// <summary>
		/// Retrieves all Documents within the project of the currently active Document.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>A collection of Documents from the same project as the active Document.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetDocumentsOfProjectOfCurrentDocument(bool includeContents);

		/// <summary>
		/// Retrieves all Documents within the project of a Document selected by the user.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>A collection of Documents from the project of the selected Document.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetDocumentsOfProjectOfSelectedDocument(bool includeContents);

		/// <summary>
		/// Retrieves all Documents currently selected in the Solution Explorer.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>A collection of Documents selected in the Solution Explorer.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetDocumentsSelectedInExplorer(bool includeContents);

		/// <summary>
		/// Retrieves all Documents that are currently open in the editor.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>A collection of open Documents.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<DocItem> GetOpenDocuments(bool includeContents);

		// Methods for getting or setting a single Document

		/// <summary>
		/// Retrieves the Document currently open and active in the editor.
		/// </summary>
		/// <param name="includeContents">`true` to include contents, `false` to get information only.</param>
		/// <returns>The active Document.</returns>
		[RiskLevel(RiskLevel.Low)]
		DocItem GetCurrentDocument(bool includeContents);

		/// <summary>
		/// Open document and make it current active document in the editor.
		/// </summary>
		/// <param name="fullName">Full path to the document.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Low)]
		bool OpenDocument(string fullName);

		/// <summary>
		/// Close document in the editor.
		/// </summary>
		/// <param name="fullName">Full path to the document.</param>
		/// <param name="save">`true` to save document before closing.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		bool CloseDocument(string fullName, bool save);

		/// <summary>
		/// Undo changes of open document in the editor.
		/// </summary>
		/// <param name="fullName">Full path to the document.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		bool UndoDocument(string fullName);

		/// <summary>
		/// Save open document in the editor.
		/// </summary>
		/// <param name="fullName">Full path to the document.</param>
		/// <param name="newFullName">Full path to the new copy of the document.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		bool SaveDocument(string fullName, string newFullName);

		/// <summary>
		/// Sets the content of the currently open and active Document in the editor.
		/// </summary>
		/// <param name="contents">The Document to be made active.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		bool SetCurrentDocumentContents(string contents);

		/// <summary>
		/// Retrieves the text currently selected within the active Document.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		DocItem GetSelection();

		/// <summary>
		/// Sets the selection within the active Document based on the provided data.
		/// </summary>
		/// <param name="contents">The selection data to be applied to the active Document.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		bool SetSelection(string contents);

		// Methods for getting errors

		/// <summary>
		/// Retrieves a list of errors. Results can by filtered.
		/// </summary>
		/// <param name="errorLevel">The error level to filter by. If null, all error levels are considered.</param>
		/// <param name="project">The specific project to filter errors by. If null, errors from all projects are considered.</param>
		/// <param name="fileName">The specific file name to filter errors by. If null, errors from all files are considered.</param>
		/// <param name="includeDocItem">If true, includes the document item associated with each error.</param>
		/// <param name="includeDocItemContents">If true, additionally loads the contents of the document item associated with each error. This parameter is effective only if <paramref name="includeDocItem"/> is also true.</param>
		/// <returns>A list of <see cref="ErrorItem"/> objects representing the filtered errors.</returns>
		[RiskLevel(RiskLevel.Low)]
		IList<ErrorItem> GetErrors(
			ErrorLevel? errorLevel = null,
			string project = null,
			string fileName = null,
			bool includeDocItem = false,
			bool includeDocItemContents = false
		);

		/// <summary>
		/// Retrieves errors currently selected in the Error List Window.
		/// </summary>
		/// <param name="includeDocItem">Includes the Document associated with the errors.</param>
		/// <param name="includeDocItemContents">Includes Document file contents.</param>
		[RiskLevel(RiskLevel.Low)]
		IList<ErrorItem> GetSelectedErrors(bool includeDocItem, bool includeDocItemContents);

		/// <summary>
		/// Retrieves information about the currently caught exception, if any.
		/// </summary>
		/// <param name="includeDocItems">Includes the Document associated with the Exception.</param>
		/// <param name="includeDocItemsContents">Includes Document file contents.</param>
		[RiskLevel(RiskLevel.Low)]
		ExceptionInfo GetCurrentException(bool includeDocItems, bool includeDocItemsContents);

		// Methods for formatting code

		/// <summary>
		/// Formats the entire content of the currently active Document according to the solution's code style settings.
		/// </summary>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Low)]
		bool EditFormatDocument();

		/// <summary>
		/// Formats the currently selected code fragment in the active Document according to the solution's code style settings.
		/// </summary>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.Low)]
		bool EditFormatSelection();

		/// <summary>
		/// Get information about current Visual Studio environment.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		Dictionary<string, JsonElement> GetEnvironmentContext();

		/// <summary>
		/// Triggers a build for the specified project within the solution.
		/// </summary>
		/// <param name="fullName">The full name of the project to build.</param>
		/// <returns>A string indicating the build result.</returns>
		[RiskLevel(RiskLevel.High)]
		string BuildSolutionProject(string fullName);

		/// <summary>
		/// Retrieves the content of a specified output window pane in Visual Studio.
		/// </summary>
		/// <param name="type">The type of the output pane (e.g., "Build" or "Debug").</param>
		/// <returns>The content of the specified output window pane.</returns>
		[RiskLevel(RiskLevel.High)]
		string GetOutputContent(string type);

	}
}
