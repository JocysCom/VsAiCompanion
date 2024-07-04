using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace JocysCom.VS.AiCompanion.Engine
{
	public class UiPresetsManager
	{

		public static Dictionary<string, VisibilityItem> AllUiElements = new Dictionary<string, VisibilityItem>();

		public static void InitControl(
			FrameworkElement root, bool includeTop = false,
				FrameworkElement[] excludeElements = null
			)
		{
			lock (AllUiElements)
			{
				var namedElements = GetDirectElementsWithNameProperty(root, includeTop);
				var newPaths = namedElements.Keys.Except(AllUiElements.Keys).ToArray();
				if (excludeElements != null)
				{
					var excludePaths = excludeElements.Select(x => GetControlPath(x));
					newPaths = namedElements.Keys.Except(excludePaths).ToArray();
				}
				// Add new elements to dictionary.
				foreach (var newPath in newPaths)
				{
					var element = namedElements[newPath];
					var item = GetVisibilityItem(newPath, element);
					AllUiElements.Add(newPath, item);
				}
				// Create sorted lit of new paths.
				var paths = Global.VisibilityPaths.Union(newPaths).ToArray();
				Array.Sort(paths);
				CollectionsHelper.Synchronize(paths, Global.VisibilityPaths);
				if (newPaths.Any())
				{
					// Apply UI preset to newly loaded controls.
					ApplyUiPreset(Global.AppSettings.UiPresetName, newPaths);
				}
			}
		}

		private static VisibilityItem GetVisibilityItem(string path, FrameworkElement element)
		{
			var item = new VisibilityItem
			{
				Path = path,
				// store default values.
				Element = element,
				IsVisible = element.Visibility == Visibility.Visible,
				IsEnabled = element.IsEnabled,
			};
			return item;
		}

		public static void ApplyUiPreset(string presetName, string[] paths)
		{
			var uiPreset = Global.UiPresets.Items.FirstOrDefault(x => x.Name == presetName);
			if (uiPreset == null)
				return;
			foreach (var path in paths)
			{
				var vi = AllUiElements[path];
				var isVisible = vi.IsVisible;
				var isEnabled = vi.IsEnabled;
				// Try to override values with preset item.
				var presetItem = uiPreset.Items.FirstOrDefault(x => x.Path == path);
				if (presetItem != null)
				{
					isVisible = presetItem.IsVisible;
					isEnabled = presetItem.IsEnabled;
				}
				var viIsVisible = vi.Element.Visibility == Visibility.Visible;
				if (viIsVisible != isVisible)
					vi.Element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
				var viIsEnabled = vi.Element.IsEnabled;
				if (viIsEnabled != isEnabled)
					vi.Element.IsEnabled = isEnabled;
			}
		}

		/// <summary>
		/// Get all child controls with x:Name or Name property set.
		/// Excludes controls that are sourced from external XAML resource dictionaries.
		/// </summary>
		/// <param name="root">The root control.</param>
		private static Dictionary<string, FrameworkElement> GetDirectElementsWithNameProperty(FrameworkElement root, bool includeTop = false)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));
			var elements = new List<FrameworkElement>();
			AppHelper.GetAllInternal(root, elements);
			if (includeTop)
				elements.Insert(0, root);
			var namedElements = elements
				.Where(x => !string.IsNullOrEmpty(x.Name))
				.ToArray();
			var dic = new Dictionary<string, FrameworkElement>();
			foreach (var ne in namedElements)
			{
				var path = GetControlPath(ne);
				if (!dic.ContainsKey(path))
					dic.Add(path, ne);
			}
			return dic;

		}

		public static string GetControlPath(FrameworkElement ne)
		{
			var nodes = new List<string>();
			FrameworkElement parent = ne;
			do
			{
				var add =
					!string.IsNullOrEmpty(parent.Name) &&
					(parent == ne || parent is UserControl) && parent != Global.MainControl;
				if (add)
					nodes.Insert(0, parent.Name);
				parent = ControlsHelper.GetParent<FrameworkElement>(parent);
			} while (parent != null);
			var path = string.Join(".", nodes);
			return path;
		}

		/// <summary>
		/// Remove controls from the UI presets list.
		/// </summary>
		public static void RemoveControls(params FrameworkElement[] controls)
		{
			foreach (var control in controls)
			{
				if (control == null)
					continue;
				var path = GetControlPath(control);
				if (AllUiElements.ContainsKey(path))
					AllUiElements.Remove(path);
			}
		}

		public static void AddControls(params FrameworkElement[] controls)
		{
			foreach (var control in controls)
			{
				if (control == null)
					continue;
				var path = GetControlPath(control);
				var item = GetVisibilityItem(path, control);
				if (!AllUiElements.ContainsKey(path))
					AllUiElements.Add(path, item);
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
