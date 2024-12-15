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
	public class HttpClientLogger : DelegatingHandler
	{
		public HttpClientLogger() : base(new HttpClientHandler()) { }

		public HttpClientLogger(HttpMessageHandler innerHandler) : base(innerHandler) { }

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

			// Log the request headers and other info if needed
			RequestCollection = await WebHelper.ToCollection(Request).ConfigureAwait(continueOnCapturedContext);

			try
			{
				if (cancellationToken.IsCancellationRequested)
					throw new TaskCanceledException();

				// Send the request
				Response = await base.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext);

				// Replace the response content with the logging content
				if (Response.Content != null)
				{
					var loggingContent = new LoggingHttpContent(Response.Content);
					Response.Content = loggingContent;
				}
			}
			catch (Exception ex)
			{
				_ = ex;
				AddTraceLog(TraceEventType.Critical);
				throw;
			}

			// Log the response headers, status code, etc., but avoid reading the content now
			ResponseCollection = await WebHelper.ToCollection(Response, request.GetHashCode()).ConfigureAwait(continueOnCapturedContext);

			if (Response == null || !Response.IsSuccessStatusCode)
				AddTraceLog(TraceEventType.Error);
			else
				AddTraceLog(TraceEventType.Information);

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
			  <!-- Enabling Tracing in HttpClientLogger -->
			  <system.diagnostics>
				<sources>
				  <source name="JocysCom.ClassLibrary.Web.Services.HttpClientLogger" switchValue="All">
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
