﻿using JocysCom.ClassLibrary.Diagnostics;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class LogHelper
	{

		#region HTTP Logging

		public static bool LogHttp
		{
			get => _LogHttp;
			set
			{
				_LogHttp = value;
				if (_LogHttp)
					EnableHttpLogging();
				else
					DisableHttpLogging();
			}
		}
		private static bool _LogHttp;

		#endregion

		private static RollingXmlWriterTraceListener logListener;

		static readonly object LogHttpLock = new object();

		public static void EnableHttpLogging()
		{
			lock (LogHttpLock)
			{
				// Make sure that Visual Studio and standalone app is not loggin to the same file.
				int processId = Process.GetCurrentProcess().Id;
				var filename = Path.Combine(Global.LogsPath, $"HttpClientLog_P{processId}.svclog");
				var sourceName = typeof(JocysCom.ClassLibrary.Web.Services.HttpClientLogger).FullName;
				logListener = new RollingXmlWriterTraceListener(filename);
				// Accept messages from `HttpClientLogger` only.
				logListener.Filter = new SourceFilter(sourceName);
				Trace.Listeners.Add(logListener);
				TraceHelper.AddLog(sourceName, TraceEventType.Information, new NameValueCollection { { "Log", "HTTP Logging Enabled" } });
				Trace.AutoFlush = true;
			}
		}

		public static void DisableHttpLogging()
		{
			lock (LogHttpLock)
			{
				if (logListener == null)
					return;
				var sourceName = typeof(JocysCom.ClassLibrary.Web.Services.HttpClientLogger).FullName;
				TraceHelper.AddLog(sourceName, TraceEventType.Information, new NameValueCollection { { "Log", "HTTP Logging Disabled" } });
				Trace.Listeners.Remove(logListener);
				logListener.Close();
				logListener = null;
			}
		}

	}
}
