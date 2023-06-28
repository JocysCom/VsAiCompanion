using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class ExceptionInfo
	{

		public ExceptionInfo() { }

		public ExceptionInfo(System.Exception exception) {
			Message = exception.Message;
			Details = exception.ToString();

		}

		public string Message { get; set; }
		public string Details { get; set; }

		public List<StackFrameInfo> StackFrames { get; set; } = new List<StackFrameInfo>();

		public static List<ExceptionInfo> Convert(System.Exception exception)
		{
			var list = new List<ExceptionInfo>();
			// return inner exceptions as a list.
			return list;
		}


	}
}
