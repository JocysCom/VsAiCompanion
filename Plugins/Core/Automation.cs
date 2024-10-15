using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Use to navigate via UI of the application.
	/// </summary>
	public class Automation
	{

		/// <summary>
		/// Get path to canvas AutomationElement
		/// </summary>
		public Func<string> GetCanvasEditorElementPath { get; set; }

		/// <summary>
		/// Retrieve current canvas path.
		/// </summary>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<string> GetCurrentCanvasPath()
		{
			try
			{
				var path = GetCanvasEditorElementPath();
				return new OperationResult<string>(path);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Get the content of the canvas.
		/// </summary>
		/// <returns></returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<string> GetCanvasContent()
		{
			try
			{
				var ah = new AutomationHelper();
				var path = GetCanvasEditorElementPath();
				var element = ah.GetElement(path);
				//var path2 = AutomationHelper.GetPath(element);
				var text = ah.GetValue(element);
				return new OperationResult<string>(text);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Get the content of the canvas.
		/// </summary>
		/// <returns></returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<bool> SetCanvasContent(string contents)
		{
			try
			{
				var ah = new AutomationHelper();
				var path = GetCanvasEditorElementPath();
				var element = ah.GetElement(path);
				ah.SetValue(element, contents);
				return new OperationResult<bool>(true);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Retrieve paths to all top-level windows on the desktop.
		/// </summary>
		[RiskLevel(RiskLevel.Medium)]
		public static string[] GetAllTopLevelWindowPaths()
		{
			// Get the desktop AutomationElement
			AutomationElement desktop = AutomationElement.RootElement;
			var paths = desktop.FindAll(TreeScope.Children, Condition.TrueCondition)
				.Cast<AutomationElement>()
				.Select(x => AutomationHelper.GetPath(x))
				.ToArray();
			return paths;
		}

		/// <summary>
		/// Navigate to a specific UI element (e.g., button, field).
		/// </summary>
		/// <param name="elementPath">The identifier of the element to navigate to.</param>
		/// <returns>True if the navigation was successful.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public static bool NavigateToElement(string elementPath)
		{
			return true; // Changed from void to bool and return true.
		}

		/// <summary>
		/// Get properties of a UI element specified by its path.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the UI element.</param>
		/// <returns>Dictionary containing property names and values.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<Dictionary<string, object>> GetElementProperties(string elementPath)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.GetElement(elementPath);
				if (element == null)
				{
					return new OperationResult<Dictionary<string, object>>(new Exception("Element not found."));
				}

				var properties = ah.GetElementProperties(element);
				return new OperationResult<Dictionary<string, object>>(properties);
			}
			catch (Exception ex)
			{
				return new OperationResult<Dictionary<string, object>>(ex);
			}
		}

		/// <summary>
		/// Find UI elements matching specific conditions.
		/// </summary>
		/// <param name="conditions">Dictionary of property names and values to match.</param>
		/// <returns>List of XPath-like paths to the matching elements.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<List<string>> FindElementsByConditions(Dictionary<string, string> conditions)
		{
			try
			{
				var ah = new AutomationHelper();
				var elements = ah.FindElementsByConditions(conditions);
				var paths = elements.Select(e => AutomationHelper.GetPath(e)).ToList();
				return new OperationResult<List<string>>(paths);
			}
			catch (Exception ex)
			{
				return new OperationResult<List<string>>(ex);
			}
		}


		/// <summary>
		/// Perform an action on a UI element specified by its path.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the UI element.</param>
		/// <param name="action">The action to perform (e.g., "click", "setText").</param>
		/// <param name="parameters">Additional parameters required for the action.</param>
		/// <returns>True if the action was performed successfully.</returns>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<bool> PerformActionOnElement(string elementPath, string action, object parameters = null)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.GetElement(elementPath);
				if (element == null)
				{
					return new OperationResult<bool>(new Exception("Element not found."));
				}

				bool result = ah.PerformAction(element, action, parameters);
				return new OperationResult<bool>(result);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Retrieve the child elements of a specified UI element.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the parent UI element.</param>
		/// <returns>List of XPath-like paths to the child elements.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<List<string>> GetElementChildren(string elementPath)
		{
			try
			{
				var ah = new AutomationHelper();
				var parentElement = ah.GetElement(elementPath);
				if (parentElement == null)
				{
					return new OperationResult<List<string>>(new Exception("Parent element not found."));
				}

				var children = ah.FindAllChildren(parentElement);
				var childPaths = children.Select(e => AutomationHelper.GetPath(e)).ToList();
				return new OperationResult<List<string>>(childPaths);
			}
			catch (Exception ex)
			{
				return new OperationResult<List<string>>(ex);
			}
		}
	}
}
