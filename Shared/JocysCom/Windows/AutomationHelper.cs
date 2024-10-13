using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace JocysCom.ClassLibrary.Windows
{
	public class AutomationHelper
	{
		#region Path Helper Methods

		/// <summary>
		/// Retrieves the XPath-like path of the given automation element.
		/// </summary>
		public static string GetPath(AutomationElement element)
		{
			if (element == null)
				return null;

			var pathSegments = new List<string>();
			var currentElement = element;

			while (currentElement != null)
			{
				var controlTypeName = currentElement.Current.ControlType.ProgrammaticName.Split('.').Last();
				var automationId = currentElement.Current.AutomationId;
				var name = currentElement.Current.Name;

				var segment = new StringBuilder(controlTypeName);
				var predicates = new List<string>();

				if (!string.IsNullOrEmpty(automationId))
					predicates.Add($"@AutomationId='{EscapeXPathValue(automationId)}'");

				if (!string.IsNullOrEmpty(name))
					predicates.Add($"@Name='{EscapeXPathValue(name)}'");

				if (predicates.Count == 0)
				{
					int index = GetIndexAmongSiblings(currentElement) + 1; // XPath is 1-based
					predicates.Add($"position()={index}");
				}

				if (predicates.Count > 0)
					segment.Append($"[{string.Join(" and ", predicates)}]");

				pathSegments.Insert(0, segment.ToString());
				currentElement = TreeWalker.RawViewWalker.GetParent(currentElement);
			}

			return "/" + string.Join("/", pathSegments);
		}

		/// <summary>
		/// Retrieves an automation element based on the provided XPath-like path.
		/// </summary>
		public AutomationElement GetElement(string path)
		{
			if (string.IsNullOrEmpty(path))
				throw new ArgumentNullException(nameof(path));

			var pathSegments = SplitXPath(path);
			var currentElement = AutomationElement.RootElement;

			foreach (var segment in pathSegments)
			{
				string controlTypeName = segment.Item1;
				var predicates = segment.Item2;

				var controlType = GetControlTypeByName(controlTypeName);

				var conditions = new List<Condition>
				{
					new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)
				};

				if (predicates.TryGetValue("AutomationId", out string automationId))
					conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

				if (predicates.TryGetValue("Name", out string name))
					conditions.Add(new PropertyCondition(AutomationElement.NameProperty, name));

				var finalCondition = conditions.Count > 1 ? new AndCondition(conditions.ToArray()) : conditions.First();

				if (predicates.TryGetValue("position()", out string positionStr) && int.TryParse(positionStr, out int position))
				{
					var children = currentElement.FindAll(TreeScope.Children, finalCondition);
					if (position > 0 && position <= children.Count)
						currentElement = children[position - 1]; // Convert to 0-based index
					else
						return null;
				}
				else
				{
					currentElement = currentElement.FindFirst(TreeScope.Children, finalCondition);
				}

				if (currentElement == null)
					return null;
			}

			return currentElement;
		}

		#endregion

		#region Element Search Methods

		/// <summary>
		/// Finds automation elements by process ID.
		/// </summary>
		public static List<AutomationElement> FindByProcessId(int id)
		{
			var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, id);
			var elements = AutomationElement.RootElement
				.FindAll(TreeScope.Element | TreeScope.Children, condition)
				.Cast<AutomationElement>()
				.ToList();
			return elements;
		}

		/// <summary>
		/// Finds window patterns by process ID.
		/// </summary>
		public static List<WindowPattern> FindWindowsByProcessId(int id)
		{
			var condition = new AndCondition(
				new PropertyCondition(AutomationElement.ProcessIdProperty, id),
				new PropertyCondition(AutomationElement.IsEnabledProperty, true),
				new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)
			);

			var windows = AutomationElement.RootElement
				.FindAll(TreeScope.Element | TreeScope.Children, condition)
				.Cast<AutomationElement>()
				.Select(x => x.GetCurrentPattern(WindowPattern.Pattern) as WindowPattern)
				.ToList();

			return windows;
		}

		/// <summary>
		/// Finds automation elements by automation ID.
		/// </summary>
		public static List<AutomationElement> FindByAutomationId(string automationId)
		{
			var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
			var elements = AutomationElement.RootElement
				.FindAll(TreeScope.Element | TreeScope.Descendants, condition)
				.Cast<AutomationElement>()
				.ToList();
			return elements;
		}

		/// <summary>
		/// Finds toolbars by process ID.
		/// </summary>
		public static List<AutomationElement> FindToolBarByProcessId(int id)
		{
			var condition = new AndCondition(
				new PropertyCondition(AutomationElement.ProcessIdProperty, id),
				new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar)
			);

			var toolbars = AutomationElement.RootElement
				.FindAll(TreeScope.Element | TreeScope.Children, condition)
				.Cast<AutomationElement>()
				.ToList();

			return toolbars;
		}

		/// <summary>
		/// Finds an element containing the specified substring.
		/// </summary>
		public AutomationElement FindElementBySubstring(AutomationElement element, ControlType controlType, string searchTerm)
		{
			var descendants = element.FindAll(
				TreeScope.Descendants,
				new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));

			foreach (AutomationElement el in descendants)
			{
				if (el.Current.Name.Contains(searchTerm))
					return el;
			}

			return null;
		}

		#endregion

		#region Toolbar and Notification Area Methods

		/// <summary>
		/// Finds the toolbar window in the notification area.
		/// </summary>
		public static AutomationElement FindToolbarWindow()
		{
			var rootElement = AutomationElement.RootElement;
			var trayWnd = FindFirstChild(rootElement, ControlType.Pane, "Shell_TrayWnd");
			var trayNotifyWnd = FindFirstChild(trayWnd, ControlType.Pane, "TrayNotifyWnd");
			var sysPager = FindFirstChild(trayNotifyWnd, ControlType.Pane, "SysPager");
			var toolbarWindow = FindFirstChild(sysPager, ControlType.ToolBar, "ToolbarWindow32");

			return toolbarWindow;
		}

		/// <summary>
		/// Finds all buttons in the toolbar window.
		/// </summary>
		public static List<AutomationElement> FindToolbarButtons()
		{
			var toolbarWindow = FindToolbarWindow();
			var buttons = FindAllChildren(toolbarWindow, ControlType.Button);
			Console.WriteLine("Number of buttons in the notification area: " + buttons.Count);
			return buttons;
		}

		#endregion

		#region Interaction Methods

		/// <summary>
		/// Simulates a click on the specified button element.
		/// </summary>
		public static void ClickButton(AutomationElement button, bool native = true)
		{
			if (button == null)
				throw new ArgumentNullException(nameof(button));

			if (native)
			{
				var rect = button.Current.BoundingRectangle;
				var x = (int)rect.Left + (int)(rect.Width / 2);
				var y = (int)rect.Top + (int)(rect.Height / 2);

				NativeMethods.SetCursorPos(x, y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, x, y, 0, 0);
			}
			else
			{
				if (button.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
				{
					((InvokePattern)pattern).Invoke();
				}
				else
				{
					throw new InvalidOperationException("The control does not support InvokePattern.");
				}
			}
		}

		/// <summary>
		/// Waits for a window to be ready for input.
		/// </summary>
		private static WindowPattern WaitForWindowToBeReady(AutomationElement targetControl)
		{
			if (targetControl == null)
				throw new ArgumentNullException(nameof(targetControl));

			try
			{
				var windowPattern = targetControl.GetCurrentPattern(WindowPattern.Pattern) as WindowPattern;
				if (windowPattern == null)
					return null;

				if (!windowPattern.WaitForInputIdle(10000))
					return null;

				return windowPattern;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>
		/// Waits for the specified window to close.
		/// </summary>
		public static void WaitForWindowToClose(WindowPattern windowPattern)
		{
			if (windowPattern == null)
				throw new ArgumentNullException(nameof(windowPattern));

			while (windowPattern.Current.WindowInteractionState != WindowInteractionState.Closing)
			{
				Task.Delay(100).Wait();
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Finds all child elements matching the specified control type.
		/// </summary>
		public static List<AutomationElement> FindAllChildren(AutomationElement parent, ControlType controlType = null)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));

			Condition condition = controlType != null
				? new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)
				: Condition.TrueCondition;

			var children = parent.FindAll(TreeScope.Children, condition);
			if (children.Count == 0)
				Console.WriteLine("Warning: Unable to find child elements with specified conditions.");

			return children.Cast<AutomationElement>().ToList();
		}

		/// <summary>
		/// Finds the first child element matching the specified conditions.
		/// </summary>
		public static AutomationElement FindFirstChild(
			AutomationElement parent,
			ControlType controlType = null,
			string className = null,
			string automationId = null,
			int? processId = null)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));

			var conditions = new List<Condition>();

			if (controlType != null)
				conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));

			if (className != null)
				conditions.Add(new PropertyCondition(AutomationElement.ClassNameProperty, className));

			if (automationId != null)
				conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

			if (processId.HasValue)
				conditions.Add(new PropertyCondition(AutomationElement.ProcessIdProperty, processId.Value));

			Condition condition;
			if (conditions.Count == 0)
			{
				condition = Condition.TrueCondition;
			}
			else if (conditions.Count == 1)
			{
				condition = conditions.First();
			}
			else
			{
				condition = new AndCondition(conditions.ToArray());
			}

			var child = parent.FindFirst(TreeScope.Children, condition);
			if (child == null)
				Console.WriteLine("Error: Unable to find child element with specified conditions.");

			return child;
		}

		/// <summary>
		/// Retrieves all child controls and their paths.
		/// </summary>
		public static Dictionary<AutomationElement, string> GetAll(AutomationElement control, string path, bool includeTop = false)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			var controls = new Dictionary<AutomationElement, string>();

			if (includeTop && !controls.ContainsKey(control))
				controls.Add(control, path);

			var children = FindAllChildren(control);
			foreach (var child in children)
			{
				var childPath = $"{path}.{child.Current.ControlType.ProgrammaticName.Split('.').Last()}";
				var childControls = GetAll(child, childPath, true);

				foreach (var kvp in childControls)
				{
					if (!controls.ContainsKey(kvp.Key))
						controls.Add(kvp.Key, kvp.Value);
				}
			}

			return controls;
		}

		#endregion

		#region Private Helper Methods

		private static List<Tuple<string, Dictionary<string, string>>> SplitXPath(string path)
		{
			var segments = new List<Tuple<string, Dictionary<string, string>>>();
			var parts = path.Trim('/').Split('/');

			foreach (var part in parts)
			{
				var match = Regex.Match(part, @"^(?<controlType>\w+)(\[(?<predicates>.+?)\])?$");

				if (match.Success)
				{
					var controlTypeName = match.Groups["controlType"].Value;
					var predicateStr = match.Groups["predicates"].Value;
					var predicates = ParsePredicates(predicateStr);
					segments.Add(Tuple.Create(controlTypeName, predicates));
				}
			}

			return segments;
		}

		private static Dictionary<string, string> ParsePredicates(string predicateStr)
		{
			var predicates = new Dictionary<string, string>();

			if (string.IsNullOrEmpty(predicateStr))
				return predicates;

			var parts = predicateStr.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var part in parts)
			{
				var kvpMatch = Regex.Match(part, @"@(?<key>\w+)=['""](?<value>.*?)['""]");
				if (kvpMatch.Success)
				{
					var key = kvpMatch.Groups["key"].Value;
					var value = UnescapeXPathValue(kvpMatch.Groups["value"].Value);
					predicates[key] = value;
				}
				else if (part.StartsWith("position()="))
				{
					var positionValue = part.Substring("position()=".Length);
					predicates["position()"] = positionValue;
				}
			}

			return predicates;
		}

		private static ControlType GetControlTypeByName(string typeName)
		{
			var fields = typeof(ControlType).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			foreach (var field in fields)
			{
				if (field.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
					return (ControlType)field.GetValue(null);
			}

			return ControlType.Custom;
		}

		private static int GetIndexAmongSiblings(AutomationElement element)
		{
			var parent = TreeWalker.RawViewWalker.GetParent(element);
			if (parent == null)
				return 0;  // Root element
			var controlType = element.Current.ControlType;
			var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
			var siblings = parent.FindAll(TreeScope.Children, condition);
			for (int index = 0; index < siblings.Count; index++)
			{
				if (siblings[index].Equals(element))
					return index;
			}
			return -1; // Should not reach here
		}

		// Escapes single quotes in XPath values
		private static string EscapeXPathValue(string value)
		{
			return value.Replace("'", "&apos;");
		}

		private static string UnescapeXPathValue(string value)
		{
			return value.Replace("&apos;", "'");
		}

		#endregion

		#region Native Methods

		internal static class NativeMethods
		{
			[DllImport("user32.dll")]
			internal static extern bool SetCursorPos(int x, int y);

			[DllImport("user32.dll")]
			internal static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

			public const int MOUSEEVENTF_LEFTDOWN = 0x02;
			public const int MOUSEEVENTF_LEFTUP = 0x04;
		}

		#endregion
	}
}
