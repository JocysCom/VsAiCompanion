using JocysCom.ClassLibrary.Diagnostics;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Web.Services
{
	// DelegatingHandler Requires: System.Net.Http.dll assembly.
	public class HttpClientSpy : DelegatingHandler
	{
		public HttpClientSpy() : base(new HttpClientHandler()) { }

		public HttpClientSpy(HttpMessageHandler innerHandler) : base(innerHandler) { }

		/// <summary>
		/// Set to false, because we don’t need anything from the current SynchronizationContext context.
		/// </summary>
		private bool continueOnCapturedContext = false;

		public HttpRequestMessage Request;
		public HttpResponseMessage Response;

		public NameValueCollection RequestCollection;
		public NameValueCollection ResponseCollection;

		protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Request = request;
			// Convert to collection now before it is disposed.
			RequestCollection = await WebHelper.ToCollection(Request).ConfigureAwait(continueOnCapturedContext);
			try
			{
				Response = await base.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext);
				// Convert to collection now before it is disposed.
				ResponseCollection = await WebHelper.ToCollection(Response, request.GetHashCode()).ConfigureAwait(continueOnCapturedContext);
			}
			catch (Exception ex)
			{
				_ = ex;
				AddTraceLog(TraceEventType.Critical);
				throw;
			}
			if (Response is null || !Response.IsSuccessStatusCode)
				AddTraceLog(TraceEventType.Error);
			else
				AddTraceLog(TraceEventType.Information);
			// Note: TaskCompletionSource creates a task that does not contain a delegate.
			//var tsc = new TaskCompletionSource<HttpResponseMessage>();
			// Also sets the task state to "RanToCompletion"
			//tsc.SetResult(Response);
			//return tsc.Task.Result;
			return Response;
		}

		#region Tracing

		/// <summary>
		/// Log Request and Response.
		/// Critical event if Exception,
		/// Error if no response or status code in the range 200-299. I
		/// Information is success.
		/// </summary>
		/// <param name="eventType"></param>
		public void AddTraceLog(TraceEventType eventType)
		{
			if (RequestCollection != null)
				TraceHelper.AddLog(GetType().FullName, eventType, RequestCollection);
			if (ResponseCollection != null)
				TraceHelper.AddLog(GetType().FullName, eventType, ResponseCollection);
		}

		// You can enable tracing and write raw data by adding configuration lines below.
		/*
			<configuration>
			  <system.serviceModel>
			  <!-- Enabling Tracing in Web Services -->
			  <diagnostics>
				<messageLogging
				  logMessagesAtTransportLevel="true"
				  logMessagesAtServiceLevel="true"
				  logMalformedMessages="true"
				  logEntireMessage="true"
				  maxSizeOfMessageToLog="65535000"
				  maxMessagesToLog="500"
				/>
			  </diagnostics>
			  </system.serviceModel>
			  <!-- Enabling Tracing in HttpClientSpy -->
			  <system.diagnostics>
				<sources>
				  <source name="JocysCom.ClassLibrary.Web.Services.HttpClientSpy" switchValue="All">
					<listeners>
					  <add name="WebServiceLogs"/>
					</listeners>
				  </source>
				</sources>
				<sharedListeners>
				  <!-- Files can be opened with SvcTraceViewer.exe. make sure that IIS_IUSRS group has write permissions on this folder. -->
				  <add name="WebServiceLogs" type="System.Diagnostics.XmlWriterTraceListener" initializeData="D:\LogFiles\WebServiceLogs.svclog" />
				</sharedListeners>
				   <trace autoflush="true" />
				 </system.diagnostics>
			</configuration>
		*/

		#endregion

	}
}
