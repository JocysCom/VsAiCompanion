using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal sealed class HelpUtils
  {
    private HelpUtils()
    {
    }

    public static bool IsContextHelpMessage(ref Message m)
    {
      return m.Msg == 274 && ((int) m.WParam & 65520) == 61824;
    }

    public static void TranslateContextHelpMessage(Form f, ref Message m)
    {
      Control activeControl = HelpUtils.GetActiveControl(f);
      if (activeControl == null)
        return;
      m.HWnd = activeControl.Handle;
      m.Msg = 83;
      m.WParam = IntPtr.Zero;
      NativeMethods.HELPINFO structure = new NativeMethods.HELPINFO();
      structure.iContextType = 1;
      structure.iCtrlId = f.Handle.ToInt32();
      structure.hItemHandle = activeControl.Handle;
      structure.dwContextId = 0;
      structure.MousePos.x = (int) NativeMethods.LOWORD((int) m.LParam);
      structure.MousePos.y = (int) NativeMethods.HIWORD((int) m.LParam);
      m.LParam = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.HELPINFO>(structure));
      Marshal.StructureToPtr<NativeMethods.HELPINFO>(structure, m.LParam, false);
    }

    public static Control GetActiveControl(Form f)
    {
      Control activeControl = (Control) f;
      while (activeControl is ContainerControl containerControl && containerControl.ActiveControl != null)
        activeControl = containerControl.ActiveControl;
      return activeControl;
    }
  }
}
