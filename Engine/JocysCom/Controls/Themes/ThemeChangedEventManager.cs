using System;
using System.Windows;

namespace JocysCom.ClassLibrary.Controls.Themes
{
	public class ThemeChangedEventManager : WeakEventManager
	{
		private static ThemeChangedEventManager _currentManager;

		private ThemeChangedEventManager()
		{
		}

		public static ThemeChangedEventManager CurrentManager
		{
			get
			{
				if (_currentManager == null)
				{
					_currentManager = new ThemeChangedEventManager();
					SetCurrentManager(typeof(ThemeChangedEventManager), _currentManager);
				}
				return _currentManager;
			}
		}

		// Add a handler for the ThemeChanged event
		public static void AddListener(EventHandler<ThemeChangedEventArgs> handler)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			CurrentManager.ProtectedAddHandler(null, handler);
		}

		// Remove a handler for the ThemeChanged event
		public static void RemoveListener(EventHandler<ThemeChangedEventArgs> handler)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			CurrentManager.ProtectedRemoveHandler(null, handler);
		}

		// This method is called when the event source starts listening
		protected override void StartListening(object source)
		{
			// For static events, source is null
			ThemeHelper.ThemeChanged += OnThemeChanged;
		}

		// This method is called when the event source stops listening
		protected override void StopListening(object source)
		{
			// For static events, source is null
			ThemeHelper.ThemeChanged -= OnThemeChanged;
		}

		// Event handler that delivers the event to the listeners
		private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
		{
			DeliverEvent(null, e);
		}
	}
}
