using System;

namespace JocysCom.ClassLibrary.Processes
{
	public class KeyboardHookEventArgs : EventArgs
	{
		public KeyboardHookEventArgs(KeyboardHookStruct data)
		{
			_data = data;
		}

		KeyboardHookStruct _data;

		public KeyboardHookStruct Data { get { return _data; } }
	}
}
