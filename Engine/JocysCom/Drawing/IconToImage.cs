using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace JocysCom.ClassLibrary.Drawing
{
	/// <summary>
	/// Summary description for IconToImage.
	/// </summary>
	public class IconToImage
	{
		public IconToImage()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		public struct ICONINFO
		{
			public int fIcon;
			public int xHotspot;
			public int yHotspot;
			public readonly IntPtr hbmMask;
			public readonly IntPtr hbmColor;
		}

		public struct BITMAP
		{
			public int bmType;
			public int bmWidth;
			public int bmHeight;
			public int bmWidthBytes;
			public short bmPlanes;
			public short bmBitsPixel;
			public int bmBits;
		}

		[DllImport("user32")]
		public static extern int GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

		[DllImport("gdi32")]
		public static extern int GetObject(IntPtr hObject, int nCount, out BITMAP bitmap);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr DeleteObject(IntPtr hObject);

		public static System.Drawing.Bitmap AlphaBitmapFromIcon(System.Drawing.Icon icon)
		{
			// (Overcome Flaw #1: GDI+ Bitmap.FromHICON mangles the alpha so the .NET Bitmap.FromHicon will not work)
			// Call Win32 to get icon info with contains the icon bitmap, return null if invalid
			ICONINFO iconInfo;
			if (GetIconInfo(icon.Handle, out iconInfo) == 0)
			{
				return null;
			}

			// Get BITMAP struct from icon bitmap
			BITMAP bitmapData;
			GetObject(iconInfo.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out bitmapData);

			// If not a 32bpp then ok to use Bitmap.FromHicon
			if (bitmapData.bmBitsPixel != 32)
			{
				DeleteObject(iconInfo.hbmColor);
				DeleteObject(iconInfo.hbmMask);
				return System.Drawing.Bitmap.FromHicon(icon.Handle);
			}

			// Create .NET wrapped bitmap from ICONINFO bitmap 
			System.Drawing.Bitmap wrapBitmap = System.Drawing.Bitmap.FromHbitmap(iconInfo.hbmColor);

			// (Overcome Flaw #2: Bitmap.FromHbitmap creates a bitmap with the	correct bits but wrong pixel format)
			// Copy bit form flawed bitmap to new bitmap with correct format
			BitmapData bmData = wrapBitmap.LockBits(new System.Drawing.Rectangle(0, 0, wrapBitmap.Width, wrapBitmap.Height), ImageLockMode.ReadOnly, wrapBitmap.PixelFormat);
			System.Drawing.Bitmap dstBitmap = new System.Drawing.Bitmap(bmData.Width, bmData.Height, bmData.Stride, PixelFormat.Format32bppArgb, bmData.Scan0);
			wrapBitmap.UnlockBits(bmData);

			// Caller must dispose of bitmaps returned in ICONINFO from GetIconInfo call
			DeleteObject(iconInfo.hbmColor);
			DeleteObject(iconInfo.hbmMask);

			// Return corrected bitmap
			return dstBitmap;
		}




	}
}
