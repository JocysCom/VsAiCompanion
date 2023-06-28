using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class StackFrameInfo
	{
		public StackFrameInfo() { }

		public int LineNumber { get; set; }

		public string FilePath { get; set; }

		public StackFrameInfo StackFrame { get; set; }

		public MethodInfo MethodInfo { get; set; }


		public static List<StackFrameInfo> Convert(System.Diagnostics.StackFrame exception)
		{
			var list = new List<StackFrameInfo>();
			// return inner exceptions as a list.
			return list;
		}


	}
}
