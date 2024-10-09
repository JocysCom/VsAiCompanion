using System;
using System.Windows.Automation;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	public class TargetSelectedEventArgs : EventArgs
	{
		public AutomationElement WindowElement { get; private set; }
		public AutomationElement ControlElement { get; private set; }

		public TargetSelectedEventArgs(AutomationElement windowElement, AutomationElement controlElement)
		{
			WindowElement = windowElement;
			ControlElement = controlElement;
		}
	}
}
