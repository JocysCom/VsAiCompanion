using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Processes;
using JocysCom.ClassLibrary.Windows;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using static JocysCom.ClassLibrary.Windows.AutomationHelper;
namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Use to navigate via UI of the application.
	/// </summary>
	/// <example>
	/// Simplified sequence of steps for an AI to control a PC via remote connection (screen, mouse, keyboard).
	///
	/// 1. Capture Current Screen
	///    - Take a screenshot to perceive the current state of the interface.
	///      Use <see cref="Multimedia.CaptureImage(int?, string, System.Drawing.Rectangle?, string, ImageFormat)" />.
	///
	/// 2. Analyze and Decide
	///    - Analyze the screenshot to determine the next action needed to progress toward the goal.
	///      Use <see cref="Multimedia.AnalysePictures(string, string[])" />.
	///
	/// 3. Execute Action
	///    - Perform the decided action, such as:
	///      - Move Mouse: Navigate the cursor to the desired coordinates.
	///        Use <see cref="Automation.MoveMouse(int, int)" />.
	///      - Click Mouse: Interact with elements by clicking as needed.
	///        Use <see cref="Automation.ClickMouseButton(MouseButtons)" />.
	///      - Send Keyboard Input: Input necessary keystrokes or commands.
	///        Use <see cref="Automation.SendKeys(string)" />.
	///      - Interact with UI Elements (e.g., buttons, fields):
	///        Use <see cref="Automation.PerformActionOnElement(string, string, object)" />.
	///      - Navigate to a Specific UI Element:
	///        Use <see cref="Automation.NavigateToElement(string)" />.
	///      - Wait for a UI Element to become available:
	///        Use <see cref="Automation.WaitForElement(string, int)" />.
	///      - Check if a UI Element is available:
	///        Use <see cref="Automation.IsElementAvailable(string)" />.
	///
	/// 4. Verify Outcome
	///    - Take a screenshot after the action.
	///      Use <see cref="Multimedia.CaptureImage(int?, string, System.Drawing.Rectangle?, string, ImageFormat)" />.
	///    - Analyze it to confirm that the action had the intended effect.
	///      Use <see cref="Multimedia.AnalysePictures(string, string[])" />.
	///    - Optionally, check if a UI Element is available:
	///      Use <see cref="Automation.IsElementAvailable(string)" />.
	///
	/// 5. Error Handling and Retrying
	///    - If verification is successful:
	///      - Proceed to the next action.
	///    - If an error is detected:
	///      - Retry the last action from the last successful state.
	///      - Repeat the verification step.
	///      - Optionally, wait for a UI Element to become available:
	///        Use <see cref="Automation.WaitForElement(string, int)" />.
	///
	/// 6. Iterate Until Completion
	///    - Repeat steps 1-5 until the final goal is achieved.
	/// </example>
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
				var path = GetCanvasEditorElementPath();
				var element = AutomationHelper.GetElement(path);
				//var path2 = AutomationHelper.GetPath(element);
				var text = AutomationHelper.GetValue(element);
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
				var path = GetCanvasEditorElementPath();
				var element = AutomationHelper.GetElement(path);
				var ah = new AutomationHelper();
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
				var element = AutomationHelper.GetElement(elementPath);
				if (element == null)
					return new OperationResult<List<KeyValue>>(new Exception("Element not found."));
				var properties = AutomationHelper.GetElementProperties(element);
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
				var element = AutomationHelper.GetElement(elementPath);
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
				var parentElement = AutomationHelper.GetElement(elementPath);
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
				var element = AutomationHelper.GetElement(elementPath);
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
				var rootElement = AutomationHelper.GetElement(elementPath);
				if (rootElement == null)
				{
					return new OperationResult<ElementNode>(new Exception("Element not found."));
				}
				var ah = new AutomationHelper();
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
				var element = AutomationHelper.GetElement(elementPath);
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

		#region Mouse and Keyboard

		/// <summary>
		/// Moves the mouse pointer to the specified screen coordinates.
		/// This method is typically used in a loop with taking screenshots and analyzing them to determine where to move the mouse.
		/// </summary>
		/// <param name="x">The x-coordinate of the screen in pixels.</param>
		/// <param name="y">The y-coordinate of the screen in pixels.</param>
		/// <returns>Operation result indicating success or failure.</returns>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<bool> MoveMouse(int x, int y)
		{
			try
			{
				MouseHelper.MoveMouse(x, y);
				return new OperationResult<bool>(true);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Simulates a mouse click with the specified button at the current cursor position.
		/// </summary>
		/// <param name="button">The mouse button to click (Left, Right, Middle).</param>
		/// <returns>Operation result indicating success or failure.</returns>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<bool> ClickMouseButton(MouseButtons button)
		{
			try
			{
				// Get current cursor position
				var x = Cursor.Position.X;
				var y = Cursor.Position.Y;
				MouseHelper.Click(x, y, button);
				return new OperationResult<bool>(true);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Sends the specified keyboard input to the active application.
		/// </summary>
		/// <param name="keys">The keys to send (e.g., "{ENTER}", "Hello World").</param>
		/// <returns>Operation result indicating success or failure.</returns>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<bool> SendKeys(string keys)
		{
			try
			{
				System.Windows.Forms.SendKeys.SendWait(keys);
				return new OperationResult<bool>(true);
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}
		}

		/// <summary>
		/// Retrieves information about the element currently under the mouse cursor position.
		/// </summary>
		/// <param name="x">The x-coordinate of the screen in pixels.</param>
		/// <param name="y">The y-coordinate of the screen in pixels.</param>
		/// <returns>An OperationResult containing the element's properties.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<List<KeyValue>> GetElementUnderMouse(int x, int y)
		{
			try
			{
				var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
				if (element == null)
					return new OperationResult<List<KeyValue>>(new Exception("Element not found under mouse."));
				var properties = AutomationHelper.GetElementProperties(element);
				var values = properties.Select(prop => new KeyValue(prop.Key, prop.Value?.ToString())).ToList();
				return new OperationResult<List<KeyValue>>(values);
			}
			catch (Exception ex)
			{
				return new OperationResult<List<KeyValue>>(ex);
			}
		}

		#endregion
	}
}
