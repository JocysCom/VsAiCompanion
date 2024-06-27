using JocysCom.ClassLibrary.Configuration;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class UiPresetsManager
	{

		public SettingsData<VisibilityItem> AllControls = new SettingsData<VisibilityItem>();

		private void ApplyVisibility(FrameworkElement root, string path, VisibilityState state)
		{
			var control = FindControl(root, path) as UIElement;
			if (control != null)
			{
				switch (state)
				{
					case VisibilityState.Visible:
						control.Visibility = Visibility.Visible;
						break;
					case VisibilityState.Collapsed:
						control.Visibility = Visibility.Collapsed;
						break;
					case VisibilityState.Hidden:
						control.Visibility = Visibility.Hidden;
						break;
					case VisibilityState.ReadOnly:
						// Assuming ReadOnly translates to IsEnabled = false
						if (control is UIElement uiElement)
							uiElement.IsEnabled = false;
						control.Visibility = Visibility.Visible;
						break;
				}
			}
		}

		private object FindControl(FrameworkElement root, string path)
		{
			var parts = path.Split('.');
			FrameworkElement current = root;
			foreach (var part in parts)
			{
				var property = current.GetType().GetProperty(part);
				if (property != null)
				{
					current = property.GetValue(current) as FrameworkElement;
				}
				else
				{
					return null;
				}
			}
			return current;
		}

	}


}
