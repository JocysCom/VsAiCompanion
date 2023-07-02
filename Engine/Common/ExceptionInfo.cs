using System;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class ExceptionInfo
	{

		public ExceptionInfo()
		{
			Data = new Dictionary<string, string>();
		}

		public ExceptionInfo(Exception ex)
		{
			Type = ex.GetType().Name;
			Message = ex.Message;
			StackTrace = ex.ToString();
			foreach (var key in ex.Data?.Keys)
				Data.Add($"{key}", $"{ex.Data[key]}");
		}

		public string Type { get; set; }
		public string Message { get; set; }
		public string StackTrace { get; set; }
		public Dictionary<string, string> Data { get; set; }

		//public List<StackFrameInfo> StackFrames { get; set; } = new List<StackFrameInfo>();

		public override string ToString()
		{
			return $"{Type}: {Message}\r\n{StackTrace}";
		}

	}
}
