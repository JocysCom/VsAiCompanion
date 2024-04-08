using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	internal sealed class UserPreferenceChangedHandler : IComponent, IDisposable
	{
		private Form _form;

		public UserPreferenceChangedHandler(Form form)
		{
			SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(HandleUserPreferenceChanged);
			_form = form;
		}

		~UserPreferenceChangedHandler() => Dispose(false);

		public ISite Site
		{
			get => _form.Site;
			set
			{
			}
		}

		public event EventHandler Disposed;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize((object)this);
		}

		private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			IUIService service = _form.Site != null ? _form.Site.GetService(typeof(IUIService)) as IUIService : (IUIService)null;
			if (service == null || !(service.Styles[(object)"DialogFont"] is Font style))
				return;
			_form.Font = style;
		}

		private void Dispose(bool disposing)
		{
			if (!disposing)
				return;
			SystemEvents.UserPreferenceChanged -= new UserPreferenceChangedEventHandler(HandleUserPreferenceChanged);
			if (Disposed == null)
				return;
			Disposed((object)this, EventArgs.Empty);
		}
	}
}
