using System.Drawing;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal sealed class LayoutUtils
  {
    private LayoutUtils()
    {
    }

    public static int GetPreferredLabelHeight(Label label)
    {
      return LayoutUtils.GetPreferredLabelHeight(label, label.Width);
    }

    public static int GetPreferredLabelHeight(Label label, int requiredWidth)
    {
      using (Graphics dc = Graphics.FromHwnd(label.Handle))
        return label.UseCompatibleTextRendering ? dc.MeasureString(label.Text, label.Font, label.Width).ToSize().Height : TextRenderer.MeasureText((IDeviceContext) dc, label.Text, label.Font, new Size(requiredWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
    }

    public static void MirrorControl(Control c)
    {
      c.Left = c.Parent.Right - c.Parent.Padding.Left - c.Margin.Left - c.Width;
      if ((c.Anchor & AnchorStyles.Left) != AnchorStyles.None && (c.Anchor & AnchorStyles.Right) != AnchorStyles.None)
        return;
      c.Anchor &= ~AnchorStyles.Left;
      c.Anchor |= AnchorStyles.Right;
    }

    public static void MirrorControl(Control c, Control pivot)
    {
      c.Left = pivot.Right - c.Width;
      if ((c.Anchor & AnchorStyles.Left) != AnchorStyles.None && (c.Anchor & AnchorStyles.Right) != AnchorStyles.None)
        return;
      c.Anchor &= ~AnchorStyles.Left;
      c.Anchor |= AnchorStyles.Right;
    }

    public static void UnmirrorControl(Control c)
    {
      c.Left = c.Parent.Left + c.Parent.Padding.Left + c.Margin.Left;
      if ((c.Anchor & AnchorStyles.Left) != AnchorStyles.None && (c.Anchor & AnchorStyles.Right) != AnchorStyles.None)
        return;
      c.Anchor &= ~AnchorStyles.Right;
      c.Anchor |= AnchorStyles.Left;
    }

    public static void UnmirrorControl(Control c, Control pivot)
    {
      c.Left = pivot.Left;
      if ((c.Anchor & AnchorStyles.Left) != AnchorStyles.None && (c.Anchor & AnchorStyles.Right) != AnchorStyles.None)
        return;
      c.Anchor &= ~AnchorStyles.Right;
      c.Anchor |= AnchorStyles.Left;
    }
  }
}
