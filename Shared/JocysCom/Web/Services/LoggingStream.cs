using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace JocysCom.ClassLibrary.Web.Services
{
	public class LoggingStream : Stream
	{
		private readonly Stream _innerStream;
		private readonly Stream _logStream;

		public LoggingStream(Stream innerStream, Stream logStream)
		{
			_innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
			_logStream = logStream ?? throw new ArgumentNullException(nameof(logStream));
		}

		public override bool CanRead => _innerStream.CanRead;
		public override bool CanSeek => false; // Adjust accordingly
		public override bool CanWrite => _innerStream.CanWrite;
		public override long Length => _innerStream.Length;

		public override long Position
		{
			get => _innerStream.Position;
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
			_innerStream.Flush();
			_logStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int bytesRead = _innerStream.Read(buffer, offset, count);
			if (bytesRead > 0)
			{
				_logStream.Write(buffer, offset, bytesRead);
				_logStream.Flush();
			}
			return bytesRead;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
			if (bytesRead > 0)
			{
				await _logStream.WriteAsync(buffer, offset, bytesRead, cancellationToken);
				await _logStream.FlushAsync(cancellationToken);
			}
			return bytesRead;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			_innerStream.Write(buffer, offset, count);
			_logStream.Write(buffer, offset, count);
			_logStream.Flush();
		}

		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
			await _logStream.WriteAsync(buffer, offset, count, cancellationToken);
			await _logStream.FlushAsync(cancellationToken);
		}

		// Implement other required abstract members...

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();
	}
}
