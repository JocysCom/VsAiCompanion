using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;

namespace JocysCom.VS.AiCompanion.Extension
{
	public partial class SolutionHelper
	{

		/// <inheritdoc/>
		public string GetOutputContent(string type)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			DTE2 dte = GetCurrentService();
			if (dte == null)
				return "Unable to retrieve the DTE service.";
			try
			{
				// Retrieve the Output window
				var outputWindow = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
				if (outputWindow == null)
					return "Output window not found.";
				// Output window pane
				var panes = ((OutputWindow)outputWindow.Object).OutputWindowPanes;
				OutputWindowPane pane = null;
				try
				{
					// Attempt to find the pane by its name
					pane = panes.Item(type);
				}
				catch (ArgumentException)
				{
					// Pane not found
					return $"Output pane '{type}' not found.";
				}

				if (pane == null)
					return $"Output pane '{type}' not found.";

				// Activate the pane and get its contents
				pane.Activate();
				string outputText = pane.TextDocument.StartPoint.CreateEditPoint().GetText(pane.TextDocument.EndPoint);
				return outputText;
			}
			catch (Exception ex)
			{
				return $"Failed to get content of the '{type}' output pane: {ex.Message}";
			}
		}

	}
}
