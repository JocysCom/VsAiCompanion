using JocysCom.ClassLibrary.Windows;
using System;
using System.Windows;
using System.Windows.Automation;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	public class AiWindowInfo
	{
		public string ElementPath { get; set; }
		public string SelectedText { get; set; }
		public string TextContent { get; set; }

		public void LoadInfo(Point point)
		{
			var element = AutomationElement.FromPoint(point);
			if (element == null)
				return;
			// Collect information from the element
			ElementPath = AutomationHelper.GetPath(element);
			try
			{
				TextContent = AutomationHelper.GetValue(element);
				SelectedText = AutomationHelper.GetSelectedTextFromElement(element);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
			}
		}

	}
}
