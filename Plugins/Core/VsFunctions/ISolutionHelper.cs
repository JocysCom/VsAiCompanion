using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Provides functionalities for interacting with Documents and solutions in Visual Studio,
	/// handling errors, and formatting code.
	/// </summary>
	public interface ISolutionHelper
	{
		// Methods for getting or setting multiple Documents

		/// <summary>
		/// Retrieves all Documents throughout the entire solution.
		/// </summary>
		/// <returns>A collection of all Documents within the solution.</returns>
		[RiskLevel(RiskLevel.High)]
		IList<DocItem> GetAllSolutionDocuments();

		/// <summary>
		/// Retrieves all Documents within the project of the currently active Document.
		/// </summary>
		/// <returns>A collection of Documents from the same project as the active Document.</returns>
		[RiskLevel(RiskLevel.High)]
		IList<DocItem> GetDocumentsOfProjectOfActiveDocument();

		/// <summary>
		/// Retrieves all Documents within the project of a Document selected by the user.
		/// </summary>
		/// <returns>A collection of Documents from the project of the selected Document.</returns>
		[RiskLevel(RiskLevel.High)]
		IList<DocItem> GetDocumentsOfProjectOfSelectedDocument();

		/// <summary>
		/// Retrieves all Documents currently selected in the Solution Explorer.
		/// </summary>
		/// <returns>A collection of Documents selected in the Solution Explorer.</returns>
		[RiskLevel(RiskLevel.High)]
		IList<DocItem> GetDocumentsSelectedInExplorer();

		/// <summary>
		/// Retrieves all Documents that are currently open in the editor.
		/// </summary>
		/// <returns>A collection of open Documents.</returns>
		[RiskLevel(RiskLevel.Medium)]
		IList<DocItem> GetOpenDocuments();

		// Methods for getting or setting a single Document

		/// <summary>
		/// Retrieves the Document currently active in the editor.
		/// </summary>
		/// <returns>The active Document.</returns>
		[RiskLevel(RiskLevel.Medium)]
		DocItem GetActiveDocument();

		/// <summary>
		/// Sets the content if the active Document in the editor.
		/// </summary>
		/// <param name="contents">The Document to be made active.</param>
		[RiskLevel(RiskLevel.Medium)]
		void SetActiveDocument(string contents);

		/// <summary>
		/// Retrieves the text currently selected within the active Document.
		/// </summary>
		[RiskLevel(RiskLevel.Medium)]
		DocItem GetSelection();

		/// <summary>
		/// Sets the selection within the active Document based on the provided data.
		/// </summary>
		/// <param name="contents">The selection data to be applied to the active Document.</param>
		[RiskLevel(RiskLevel.Medium)]
		void SetSelection(string contents);

		// Methods for getting errors

		/// <summary>
		/// Retrieves the error currently selected in the Error List Window.
		/// </summary>
		/// <returns>The selected error information.</returns>
		[RiskLevel(RiskLevel.Low)]
		ErrorItem GetSelectedError();

		/// <summary>
		/// Retrieves the Document associated with the error currently selected in the Error List Window.
		/// </summary>
		/// <returns>The Document containing the selected error.</returns>
		[RiskLevel(RiskLevel.Medium)]

		DocItem GetSelectedErrorDocument();

		/// <summary>
		/// Retrieves information about the currently caught exception, if any.
		/// </summary>
		/// <returns>The information about the current exception.</returns>
		[RiskLevel(RiskLevel.Medium)]
		ExceptionInfo GetCurrentException();

		/// <summary>
		/// Retrieves Documents related to the currently caught exception.
		/// </summary>
		/// <returns>A collection of Documents associated with the current exception.</returns>
		[RiskLevel(RiskLevel.Medium)]
		IList<DocItem> GetCurrentExceptionDocuments();

		// Methods for formatting code

		/// <summary>
		/// Formats the entire content of the currently active Document according to the solution's code style settings.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		void EditFormatDocument();

		/// <summary>
		/// Formats the currently selected code fragment in the active Document according to the solution's code style settings.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		void EditFormatSelection();
	}
}
