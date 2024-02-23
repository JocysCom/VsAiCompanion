using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.Collections.Generic;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Use to get selection or document content from Visual Studio.
	/// </summary>
	public partial class VisualStudio : ISolutionHelper
	{
		/// <summary>
		/// Current Visual Solution helper.
		/// </summary>
		public static ISolutionHelper Current;

		/// <inheritdoc />
		public bool EditFormatDocument()
			=> Current.EditFormatDocument();

		/// <inheritdoc />
		public bool EditFormatSelection()
			=> Current.EditFormatSelection();

		/// <inheritdoc />
		public DocItem GetCurrentDocument(bool includeContents)
			=> Current.GetCurrentDocument(includeContents);

		/// <inheritdoc />
		public bool SetCurrentDocument(string fileName)
			=> Current.SetCurrentDocument(fileName);

		/// <inheritdoc />
		public IList<DocItem> GetAllSolutionDocuments(bool includeContents)
			=> Current.GetAllSolutionDocuments(includeContents);

		/// <inheritdoc />
		public ExceptionInfo GetCurrentException(bool includeDocItem, bool includeDocItemContents)
			=> Current.GetCurrentException(includeDocItem, includeDocItemContents);

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfCurrentDocument(bool includeContents)
			=> Current.GetDocumentsOfProjectOfCurrentDocument(includeContents);

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfSelectedDocument(bool includeContents)
			=> Current.GetDocumentsOfProjectOfSelectedDocument(includeContents);

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsSelectedInExplorer(bool includeContents)
			=> Current.GetDocumentsSelectedInExplorer(includeContents);

		/// <inheritdoc />
		public IList<DocItem> GetOpenDocuments(bool includeContents)
			=> Current.GetOpenDocuments(includeContents);

		/// <inheritdoc />
		public IList<ErrorItem> GetErrors(
			ErrorLevel? errorLevel = null,
			string project = null,
			string fileName = null,
			bool includeDocItem = false,
			bool includeDocItemContents = false)
			=> Current.GetErrors(
				errorLevel,
				project,
				fileName,
				includeDocItem,
				includeDocItemContents
			);


		/// <inheritdoc />
		public IList<ErrorItem> GetSelectedErrors(bool includeDocItem, bool includeDocItemContents)
			=> Current.GetSelectedErrors(includeDocItem, includeDocItemContents);

		/// <inheritdoc />
		public DocItem GetSelection()
			=> Current.GetSelection();

		/// <inheritdoc />
		public bool SetCurrentDocumentContents(string contents)
			=> Current.SetCurrentDocumentContents(contents);

		/// <inheritdoc />
		public bool SetSelection(string contents)
			=> Current.SetSelection(contents);


		/// <inheritdoc />
		public Dictionary<string, JsonElement> GetEnvironmentContext()
			=> Current.GetEnvironmentContext();

	}
}
