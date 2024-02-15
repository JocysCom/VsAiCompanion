using System;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Exception info.
	/// </summary>
	public class ExceptionInfo
	{

		/// <summary>
		/// Exception info.
		/// </summary>
		public ExceptionInfo()
		{
			Data = new Dictionary<string, string>();
		}

		/// <summary>
		/// Exception info.
		/// </summary>
		public ExceptionInfo(Exception ex)
		{
			Type = ex.GetType().Name;
			Message = ex.Message;
			StackTrace = ex.ToString();
			foreach (var key in ex.Data?.Keys)
				Data.Add($"{key}", $"{ex.Data[key]}");
		}

		/// <summary>Exception type.</summary>
		public string Type { get; set; }

		/// <summary>Exception Message.</summary>
		public string Message { get; set; }

		/// <summary>Exception Stack trace.</summary>
		public string StackTrace { get; set; }

		/// <summary>Extended exception data.</summary>
		public Dictionary<string, string> Data { get; set; }

		//public List<StackFrameInfo> StackFrames { get; set; } = new List<StackFrameInfo>();

		/// <summary>Exception to string.</summary>
		public override string ToString()
		{
			return $"{Type}: {Message}\r\n{StackTrace}";
		}

	}
}
