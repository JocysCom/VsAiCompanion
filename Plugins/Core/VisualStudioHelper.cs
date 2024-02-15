using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Use to get selection or document content from Visual Studio.
	/// </summary>
	public partial class VisualStudioHelper : ISolutionHelper
	{
		/// <summary>
		/// Current Visual Solution helper.
		/// </summary>
		public static ISolutionHelper Current;

		/// <inheritdoc />
		public void EditFormatDocument()
			=> Current.EditFormatDocument();

		/// <inheritdoc />
		public void EditFormatSelection()
			=> Current.EditFormatSelection();

		/// <inheritdoc />
		public DocItem GetActiveDocument()
			=> Current.GetActiveDocument();

		/// <inheritdoc />
		public IList<DocItem> GetAllSolutionDocuments()
			=> Current.GetAllSolutionDocuments();

		/// <inheritdoc />
		public ExceptionInfo GetCurrentException()
			=> Current.GetCurrentException();

		/// <inheritdoc />
		public IList<DocItem> GetCurrentExceptionDocuments()
			=> Current.GetCurrentExceptionDocuments();

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfActiveDocument()
			=> Current.GetDocumentsOfProjectOfActiveDocument();

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsOfProjectOfSelectedDocument()
			=> Current.GetDocumentsOfProjectOfSelectedDocument();

		/// <inheritdoc />
		public IList<DocItem> GetDocumentsSelectedInExplorer()
			=> Current.GetDocumentsSelectedInExplorer();

		/// <inheritdoc />
		public IList<DocItem> GetOpenDocuments()
			=> Current.GetOpenDocuments();

		/// <inheritdoc />
		public ErrorItem GetSelectedError()
			=> Current.GetSelectedError();

		/// <inheritdoc />
		public DocItem GetSelectedErrorDocument()
			=> Current.GetSelectedErrorDocument();

		/// <inheritdoc />
		public DocItem GetSelection()
			=> Current.GetSelection();

		/// <inheritdoc />
		public void SetActiveDocument(string contents)
			=> Current.SetActiveDocument(contents);

		/// <inheritdoc />
		public void SetSelection(string contents)
			=> Current.SetSelection(contents);

	}
}
