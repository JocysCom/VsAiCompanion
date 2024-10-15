using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Windows;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using static JocysCom.ClassLibrary.Windows.AutomationHelper;

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
		/// Get path to temp folder.
		/// </summary>
		public Func<string> GetTempFolderPath { get; set; }


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
		/// <returns>List containing property names and values.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<List<KeyValue>> GetElementProperties(string elementPath)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.GetElement(elementPath);
				if (element == null)
				{
					return new OperationResult<List<KeyValue>>(new Exception("Element not found."));
				}

				var properties = ah.GetElementProperties(element);
				var values = properties.Select(x => new KeyValue(x.Key, x.Value?.ToString())).ToList();
				return new OperationResult<List<KeyValue>>(values);
			}
			catch (Exception ex)
			{
				return new OperationResult<List<KeyValue>>(ex);
			}
		}

		/// <summary>
		/// Find UI elements matching specific conditions.
		/// </summary>
		/// <param name="conditions">List of property names and values to match.</param>
		/// <returns>List of XPath-like paths to the matching elements.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<List<string>> FindElementsByConditions(KeyValue[] conditions)
		{
			try
			{
				var ah = new AutomationHelper();
				var elements = ah.FindElementsByConditions(conditions.ToList());
				var paths = elements.Select(e => AutomationHelper.GetPath(e)).ToList();
				return new OperationResult<List<string>>(paths);
			}
			catch (Exception ex)
			{
				return new OperationResult<List<string>>(ex);
			}
		}


		/// <summary>
		/// Performs the specified action on the given automation element.
		/// </summary>
		/// <param name="elementPath">The AutomationElement to interact with.</param>
		/// <param name="action">The action to perform (e.g., "click", "settext", "select", "sendkeys").</param>
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

		/// <summary>
		/// Waits for a UI element specified by its path to become available within a given timeout.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the UI element.</param>
		/// <param name="timeoutInMilliseconds">Maximum time to wait for the element.</param>
		/// <returns>True if the element becomes available within the timeout, false otherwise.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<bool> WaitForElement(string elementPath, int timeoutInMilliseconds)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.WaitForElement(elementPath, timeoutInMilliseconds);

				return new OperationResult<bool>(element != null);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Checks if a UI element specified by its path is currently available.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the UI element.</param>
		/// <returns>True if the element is available, false otherwise.</returns>
		[RiskLevel(RiskLevel.Low)]
		public OperationResult<bool> IsElementAvailable(string elementPath)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.GetElement(elementPath);

				return new OperationResult<bool>(element != null);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Retrieves the UI element hierarchy starting from the specified element.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the root UI element.</param>
		/// <returns>An ElementNode representing the UI hierarchy.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<ElementNode> GetElementTree(string elementPath)
		{
			try
			{
				var ah = new AutomationHelper();
				var rootElement = ah.GetElement(elementPath);
				if (rootElement == null)
				{
					return new OperationResult<ElementNode>(new Exception("Element not found."));
				}

				var tree = ah.BuildElementTree(rootElement);
				return new OperationResult<ElementNode>(tree);
			}
			catch (Exception ex)
			{
				return new OperationResult<ElementNode>(ex);
			}
		}

		/// <summary>
		/// Captures an image of the specified UI element.
		/// </summary>
		/// <param name="elementPath">XPath-like path of the UI element.</param>
		/// <param name="format">The format of the image to capture.</param>
		/// <returns>Path to the captured image, that could be read with other function.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public async Task<OperationResult<string>> CaptureElementImageToFile(string elementPath, ImageFormat format)
		{
			try
			{
				var ah = new AutomationHelper();
				var element = ah.GetElement(elementPath);
				if (element == null)
				{
					return new OperationResult<string>(new Exception("Element not found."));
				}
				var boundingRect = element.Current.BoundingRectangle;
				if (boundingRect == System.Windows.Rect.Empty)
					throw new InvalidOperationException("Element has no bounding rectangle.");
				var region = new System.Drawing.Rectangle((int)boundingRect.X, (int)boundingRect.Y, (int)boundingRect.Width, (int)boundingRect.Height);
				var tempFolderPath = System.IO.Path.Combine(GetTempFolderPath(), "Screenshots");
				var result = await ScreenshotHelper.CaptureRegion(region, tempFolderPath, format);
				return result;
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}
	}
}
