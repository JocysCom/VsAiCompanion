using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine
{

	/// <summary>
	/// Creates a context menu for elements to be added or removes from UI presets.
	/// </summary>
	public class UiContextMenuManager
	{
		public static void AssignContextMenu(FrameworkElement element)
		{
			// Skip elements that already have a context menu
			if (element.ContextMenu == null)
			{
				// Create and assign a context menu to the element
				var contextMenu = CreateContextMenuForElement(element);
				element.ContextMenu = contextMenu;
			}
		}

		private class Menus
		{
			public UiPresetItem Preset { get; set; }
			public MenuItem VisibleY { get; set; }
			public MenuItem VisibleN { get; set; }
			public MenuItem EnabledY { get; set; }
			public MenuItem EnabledN { get; set; }
		}

		private static ContextMenu CreateContextMenuForElement(FrameworkElement element)
		{
			var contextMenu = new ContextMenu();
			var path = UiPresetsManager.GetControlPath(element);
			var head = new MenuItem() { Header = element.Name, IsEnabled = false, ToolTip = path };
			contextMenu.Items.Add(head);
			foreach (var preset in Global.UiPresets.Items)
			{
				var presetMenuItem = new MenuItem { Header = preset.Name };
				contextMenu.Items.Add(presetMenuItem);

				var menus = new Menus();

				menus.Preset = preset;

				// Add submenu items for 'Visible' and 'Enabled' options
				menus.VisibleY = new MenuItem { Header = "Visible: Yes", IsCheckable = true };
				menus.VisibleN = new MenuItem { Header = "Visible: No", IsCheckable = true };
				menus.EnabledY = new MenuItem { Header = "Enabled: Yes", IsCheckable = true };
				menus.EnabledN = new MenuItem { Header = "Enabled: No", IsCheckable = true };

				// Initialize the checkboxes based on the current preset settings
				InitializeMenuItems(element, preset, menus);

				// Now attach the event handlers
				// Ensure only one of the 'Yes' or 'No' options is checked at a time
				menus.VisibleY.Checked += (s, e) => menus.VisibleN.IsChecked = false;
				menus.VisibleN.Checked += (s, e) => menus.VisibleY.IsChecked = false;
				menus.EnabledY.Checked += (s, e) => menus.EnabledN.IsChecked = false;
				menus.EnabledN.Checked += (s, e) => menus.EnabledY.IsChecked = false;

				var menuItems = new[] { menus.VisibleY, menus.VisibleN, menus.EnabledY, menus.EnabledN };
				foreach (var menuItem in menuItems)
				{
					menuItem.Checked += (s, e) => UpdatePreset(menus, element);
					menuItem.Unchecked += (s, e) => UpdatePreset(menus, element);
				}
				// Add the submenu items to the preset menu item
				presetMenuItem.Items.Add(menus.VisibleY);
				presetMenuItem.Items.Add(menus.VisibleN);
				presetMenuItem.Items.Add(menus.EnabledY);
				presetMenuItem.Items.Add(menus.EnabledN);
				// Attach the PreviewMouseRightButtonDown event handler
				element.PreviewMouseRightButtonDown += Element_PreviewMouseRightButtonDown;
			}

			return contextMenu;
		}

		private static void Element_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			var element = (FrameworkElement)sender;
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				// Ctrl+Shift is held down - assign custom context menu
				if (element.ContextMenu == null)
				{
					var contextMenu = CreateContextMenuForElement(element);
					element.ContextMenu = contextMenu;
				}
			}
			else
			{
				// Ctrl+Shift is not held - remove custom context menu
				if (element.ContextMenu != null)
					element.ContextMenu = null;
			}
		}

		private static void InitializeMenuItems(
			FrameworkElement element,
			UiPresetItem preset,
			Menus menus)
		{
			var path = UiPresetsManager.GetControlPath(element);
			var visibilityItem = preset.Items.FirstOrDefault(v => v.Path == path);
			if (visibilityItem == null)
			{
				menus.VisibleY.IsChecked = false;
				menus.VisibleN.IsChecked = false;
				menus.EnabledY.IsChecked = false;
				menus.EnabledN.IsChecked = false;
			}
			else
			{
				menus.VisibleY.IsChecked = visibilityItem.IsVisible;
				menus.VisibleN.IsChecked = !visibilityItem.IsVisible;
				menus.EnabledY.IsChecked = visibilityItem.IsEnabled;
				menus.EnabledN.IsChecked = !visibilityItem.IsEnabled;
			}
		}

		private static void UpdatePreset(Menus menus, FrameworkElement element)
		{
			var path = UiPresetsManager.GetControlPath(element);
			var menuItems = new[] { menus.VisibleY, menus.VisibleN, menus.EnabledY, menus.EnabledN };
			var preset = menus.Preset;
			var visibilityItem = preset.Items.FirstOrDefault(i => i.Path == path);
			var allUnchecked = menuItems.All(x => !x.IsChecked);
			if (visibilityItem == null)
			{
				if (allUnchecked)
					return;
				visibilityItem = UiPresetsManager.GetVisibilityItem(path, element);
				preset.Items.Add(visibilityItem);
			}
			else if (allUnchecked)
			{
				preset.Items.Remove(visibilityItem);
				visibilityItem = null;
			}
			if (visibilityItem != null)
			{
				// Update visibility.
				var isVisible = menus.VisibleY.IsChecked;
				if (visibilityItem.IsVisible != isVisible)
					visibilityItem.IsVisible = isVisible;
				// Update enabled.
				var isEnabled = menus.EnabledY.IsChecked;
				if (visibilityItem.IsEnabled != isEnabled)
					visibilityItem.IsEnabled = isEnabled;
			}
			// If this is the currently active preset then apply UI changes.
			if (Global.AppSettings.UiPresetName == preset.Name)
				UiPresetsManager.ApplyUiPreset(preset.Name, new[] { path });
		}

	}
}
