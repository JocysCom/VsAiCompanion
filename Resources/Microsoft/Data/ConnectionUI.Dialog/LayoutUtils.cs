using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

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
			return LayoutUtils.GetPreferredHeight((Control)label, label.UseCompatibleTextRendering, requiredWidth);
		}

		public static int GetPreferredCheckBoxHeight(CheckBox checkBox)
		{
			return LayoutUtils.GetPreferredHeight((Control)checkBox, checkBox.UseCompatibleTextRendering, checkBox.Width);
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

		private static int GetPreferredHeight(
		  Control c,
		  bool useCompatibleTextRendering,
		  int requiredWidth)
		{
			using (Graphics dc = Graphics.FromHwnd(c.Handle))
				return useCompatibleTextRendering ? dc.MeasureString(c.Text, c.Font, c.Width).ToSize().Height : TextRenderer.MeasureText((IDeviceContext)dc, c.Text, c.Font, new Size(requiredWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
		}
	}
}
