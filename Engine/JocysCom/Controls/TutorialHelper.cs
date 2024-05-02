using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JocysCom.ClassLibrary.Controls
{
	internal class TutorialHelper
	{
		private static Grid overlayGrid;
		private static FrameworkElement previousElement;
		private static UserControl _MainControl;

		public static void SetupTutorialHelper(UserControl mainControl)
		{
			Application.Current.MainWindow.KeyDown -= MainWindow_KeyDown;
			// Attach global key handlers to the application
			if (mainControl != null)
				Application.Current.MainWindow.KeyDown += MainWindow_KeyDown;
			_MainControl = mainControl;
		}

		public static bool Focus(FrameworkElement element, string helpText, bool enable)
		{
			if (_MainControl == null) return false;

			if (enable)
			{
				if (overlayGrid != null || element == null) return false; // A tutorial is already active, or no element is provided

				// Initialize and show the overlay
				InitializeOverlay(helpText);
				PlaceElementInOverlay(element);
			}
			else
			{
				// Remove the tutorial overlay
				RemoveOverlay();
			}
			return true;
		}

		private static void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F1)
			{
				// Assuming we are tracking the currently hovered element, for illustrative purposes, replace with actual hovered element reference
				var hoveredElement = previousElement; // Placeholder for the actual hovered element
				var helpText = "This is a demo help text."; // Replace with actual help text
				Focus(hoveredElement, helpText, true);
			}
			else if (e.Key == Key.Escape)
			{
				Focus(null, null, false);
			}
		}

		private static void InitializeOverlay(string helpText)
		{
			overlayGrid = new Grid { Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)) };
			_MainControl.Content = overlayGrid; // Assumes Global.MainControl can directly contain the overlay - adjust as necessary.

			// Place the help text
			TextBlock textBlock = new TextBlock
			{
				Text = helpText,
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 20, 0, 0) // Adjust the placement as needed
			};
			overlayGrid.Children.Add(textBlock);
		}

		private static void PlaceElementInOverlay(FrameworkElement element)
		{
			// Logic for placing the actual element within an overlay comes here
			// In practice, this involves visually highlighting or bringing the element to the foreground within the overlay
			// This can be a complex process depending on the element and the layout
			previousElement = element;
		}

		private static void RemoveOverlay()
		{
			if (overlayGrid != null && _MainControl.Content == overlayGrid)
			{
				_MainControl.Content = null; // Adjust as needed. This might involve restoring the original content hierarchy.
				overlayGrid = null;

				if (previousElement != null)
				{
					// Reset any specific styling or Z-index applied to the previously focused element
					previousElement = null;
				}
			}
		}

	}
}
