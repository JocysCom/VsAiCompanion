using System;
using System.Runtime.InteropServices;

namespace JocysCom.ClassLibrary.Controls.Chat
{
	[ComVisible(true)]
	public class ScriptingHandler
	{
		public event EventHandler<string[]> OnMessageAction;
		
		public void MessageAction(string messageId, string action, string data)
			=> OnMessageAction?.Invoke(this, new string[] { messageId, action, data });
	}
}
