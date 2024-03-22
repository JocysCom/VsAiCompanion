using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal sealed class UserPreferenceChangedHandler : IComponent, IDisposable
  {
    private Form _form;

    public UserPreferenceChangedHandler(Form form)
    {
      SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(this.HandleUserPreferenceChanged);
      this._form = form;
    }

    ~UserPreferenceChangedHandler() => this.Dispose(false);

    public ISite Site
    {
      get => this._form.Site;
      set
      {
      }
    }

    public event EventHandler Disposed;

    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize((object) this);
    }

    private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
      IUIService service = this._form.Site != null ? this._form.Site.GetService(typeof (IUIService)) as IUIService : (IUIService) null;
      if (service == null || !(service.Styles[(object) "DialogFont"] is Font style))
        return;
      this._form.Font = style;
    }

    private void Dispose(bool disposing)
    {
      if (!disposing)
        return;
      SystemEvents.UserPreferenceChanged -= new UserPreferenceChangedEventHandler(this.HandleUserPreferenceChanged);
      if (this.Disposed == null)
        return;
      this.Disposed((object) this, EventArgs.Empty);
    }
  }
}
