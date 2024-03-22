using System.Globalization;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal sealed class RTLAwareMessageBox
  {
    private RTLAwareMessageBox()
    {
    }

    public static DialogResult Show(string caption, string text, MessageBoxIcon icon)
    {
      MessageBoxOptions options = (MessageBoxOptions) 0;
      if (CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft)
        options = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading;
      return MessageBox.Show(text, caption, MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1, options);
    }
  }
}
