using System.Runtime.InteropServices;

namespace JocysCom.ClassLibrary.Processes
{
	//Declare wrapper managed KeyboardHookStruct class.
	[StructLayout(LayoutKind.Sequential)]
	public class KeyboardHookStruct
	{
		public int vkCode;  //Specifies a virtual-key code. The code must be a value in the range 1 to 254. 
		public int scanCode; // Specifies a hardware scan code for the key. 
		public int flags;  // Specifies the extended-key flag, event-injected flag, context code, and transition-state flag.
		public int time; // Specifies the time stamp for this message.
		public int dwExtraInfo; // Specifies extra information associated with the message. 
	}
}
