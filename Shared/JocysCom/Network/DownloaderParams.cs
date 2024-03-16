using System.Collections.Generic;
using System.Text;

namespace JocysCom.ClassLibrary.Network
{
	public class DownloaderParams
	{

		public DownloaderParams()
		{
			Headers = new Dictionary<string, IEnumerable<string>>();
		}

		public string SourceUrl { get; set; }
		public string TargetFile { get; set; }

		public Dictionary<string, IEnumerable<string>> Headers { get; set; }

		public int Timeout { get; set; } = 60;

		public int Retries { get; set; } = 4;

		public int RetriesLeft { get; set; } = 4;

		public int Sleep { get; set; } = 0;

		public bool Cancel { get; set; }

		public bool Success { get; set; }

		public byte[] ResponseData { get; set; }

		public Encoding ResponseEncoding { get; set; }

	}
}
