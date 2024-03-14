using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Network
{
	public class Downloader : IDisposable
	{
		private readonly HttpClient client;
		public Downloader()
		{
			Params = new DownloaderParams();
			var handler = new HttpClientHandler()
			{
				AllowAutoRedirect = true
			};
			// Assuming the use of a modern .NET version where HttpClientHandler supports automatic decompression
			handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			client = new HttpClient(handler);
			retryTimer = new System.Timers.Timer();
			retryTimer.AutoReset = false;
			retryTimer.Elapsed += RetryTimer_Elapsed;
		}

		private void RetryTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			var ts = new ThreadStart(async () => await LoadAsync());
			var t = new Thread(ts);
			t.IsBackground = true;
			t.Start();
		}

		public event EventHandler<DownloaderEventArgs> Progress;

		System.Timers.Timer retryTimer;
		public DownloaderParams Params;

		public async Task LoadAsync()
		{
			Params.RetriesLeft--;
			try
			{
				client.DefaultRequestHeaders.Clear();
				foreach (var header in Params.Headers)
				{
					client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
				}
				// Set timeout as needed.
				client.Timeout = TimeSpan.FromSeconds(Params.Timeout);
				var response = await client.GetAsync(Params.Url);
				if (response.IsSuccessStatusCode)
				{
					var contentType = response.Content.Headers.ContentType;
					Params.ResponseEncoding = contentType != null && contentType.CharSet != null
						? Encoding.GetEncoding(contentType.CharSet)
						: Params.ResponseEncoding = Encoding.UTF8;

					using (var stream = await response.Content.ReadAsStreamAsync())
					using (var ms = new MemoryStream())
					{
						await stream.CopyToAsync(ms);
						var e = new DownloaderEventArgs
						{
							BytesReceived = ms.Length,
							TotalBytesToReceive = ms.Length
						};
						Params.ResponseData = ms.ToArray();
						Progress?.Invoke(this, e);
					}
					//Params.ResponseEncoding = ???;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(":Exception " + ex.Message);
			}
		}

		#region ■ IDisposable

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				retryTimer.Dispose();
				client?.Dispose();
			}
		}

		#endregion
	}
}
