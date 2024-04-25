﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace JocysCom.ClassLibrary.Processes
{
	[StructLayout(LayoutKind.Sequential)]
	public struct CURSORINFO
	{
		/// <summary>
		/// Structure size, in bytes.
		/// </summary>
		public int Size;
		/// <summary>
		/// Cursor state.
		/// </summary>
		public int Flags;
		/// <summary>
		/// Handle to the cursor.
		/// </summary>
		public readonly IntPtr Handle;
		/// <summary>
		/// Screen coordinates of the cursor.
		/// </summary>
		public Point Position;
	}

}
