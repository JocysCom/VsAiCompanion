using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Automation;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class CanvasHelper : IDisposable
	{
		// Observable collection of window names.
		public ObservableCollection<string> WindowNames { get; } = new ObservableCollection<string>();

		// Current window and text box references.
		public AutomationElement CurrentWindow { get; private set; }
		public AutomationElement CurrentTextBox { get; private set; }

		// Synchronization context for UI thread marshaling.
		private SynchronizationContext synchronizationContext;

		// Automation event handlers.
		private AutomationEventHandler windowOpenedEventHandler;
		private AutomationEventHandler windowClosedEventHandler;

		public CanvasHelper()
		{
			synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
			WindowNames = new ObservableCollection<string>();
		}

		public void StartMonitoringWindows()
		{
			// Subscribe to WindowOpened and WindowClosed events.
			windowOpenedEventHandler = new AutomationEventHandler(OnWindowOpened);
			Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, windowOpenedEventHandler);

			windowClosedEventHandler = new AutomationEventHandler(OnWindowClosed);
			Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, TreeScope.Children, windowClosedEventHandler);

			// Initial population of the window list.
			RefreshWindowList();
		}

		public void StopMonitoringWindows()
		{
			// Unsubscribe from events.
			Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, windowOpenedEventHandler);
			Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, windowClosedEventHandler);
		}

		private void OnWindowOpened(object sender, AutomationEventArgs e)
		{
			AutomationElement window = sender as AutomationElement;
			if (window != null && ContainsTextBox(window))
			{
				// Add window to the collection.
				AddWindow(window);
			}
		}

		private void OnWindowClosed(object sender, AutomationEventArgs e)
		{
			AutomationElement window = sender as AutomationElement;
			if (window != null)
			{
				// Remove window from the collection.
				RemoveWindow(window);
			}
		}

		private void RefreshWindowList()
		{
			WindowNames.Clear();

			// Find all top-level windows.
			var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

			foreach (AutomationElement window in windows)
			{
				if (ContainsTextBox(window))
				{
					AddWindow(window);
				}
			}
		}

		private bool ContainsTextBox(AutomationElement window)
		{
			// Look for text boxes within the window.
			var textBox = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
			return textBox != null;
		}

		private void AddWindow(AutomationElement window)
		{
			string windowName = window.Current.Name;
			synchronizationContext.Post((_) =>
			{
				if (!WindowNames.Contains(windowName))
				{
					WindowNames.Add(windowName);
				}
			}, null);
		}

		private void RemoveWindow(AutomationElement window)
		{
			string windowName = window.Current.Name;
			synchronizationContext.Post((_) =>
			{
				if (WindowNames.Contains(windowName))
				{
					WindowNames.Remove(windowName);
				}
			}, null);
		}

		public void SetCurrentWindow(string windowName)
		{
			// Find and set the current window by name.
			var window = FindWindowByName(windowName);
			if (window != null)
			{
				CurrentWindow = window;

				// Find and set the current text box within the window.
				var textBox = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
				CurrentTextBox = textBox;
			}
			else
			{
				CurrentWindow = null;
				CurrentTextBox = null;
			}
		}

		private AutomationElement FindWindowByName(string windowName)
		{
			// Find window with the specified name.
			var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

			foreach (AutomationElement window in windows)
			{
				if (window.Current.Name.Equals(windowName, StringComparison.CurrentCultureIgnoreCase))
				{
					return window;
				}
			}
			return null;
		}

		public void Dispose()
		{
			StopMonitoringWindows();
		}
	}
}
