using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>Transcription text.</summary>
	public class @transcription
	{
		/// <summary>Transcription text.</summary>
		public string text { get; set; }

		/// <summary>Language</summary>
		public string language { get; set; }

		/// <summary>Duration</summary>
		public TimeSpan? duration { get; set; }


	}
}
