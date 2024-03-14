﻿using System;
namespace JocysCom.ClassLibrary.Network
{
	public class DownloaderEventArgs : EventArgs
	{
		public long BytesReceived { get; set; }
		public long TotalBytesToReceive { get; set; }
	}
}
