using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System;

namespace JocysCom.ClassLibrary.Web.Services
{
	public class LoggingHttpContent : HttpContent
	{
		private readonly HttpContent _originalContent;
		private readonly MemoryStream _logStream;

		public LoggingHttpContent(HttpContent originalContent)
		{
			_originalContent = originalContent ?? throw new ArgumentNullException(nameof(originalContent));
			_logStream = new MemoryStream();

			// Copy headers
			foreach (var header in _originalContent.Headers)
			{
				Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
		}

		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
		{
			// Create a LoggingStream that wraps the destination stream
			using (var loggingStream = new LoggingStream(stream, _logStream))
			{
				await _originalContent.CopyToAsync(loggingStream);
			}
		}

		protected override bool TryComputeLength(out long length)
		{
			length = _originalContent.Headers.ContentLength ?? -1;
			return length >= 0;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_originalContent.Dispose();
				_logStream.Dispose();
			}
			base.Dispose(disposing);
		}

		// Provide a way to access the logged content after it's read
		public async Task<string> GetLoggedContentAsync()
		{
			_logStream.Position = 0;
			using (var reader = new StreamReader(_logStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
			{
				return await reader.ReadToEndAsync();
			}
		}
	}
}
