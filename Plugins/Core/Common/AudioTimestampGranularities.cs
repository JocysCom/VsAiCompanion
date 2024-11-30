using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Specifies the timestamp granularities to populate for a transcription.
	/// </summary>
	[Flags]
	public enum audio_timestamp_granularities
	{
		/// <summary>
		/// The default value that, when equivalent to a request's flags, specifies no specific audio timestamp granularity
		/// and defers to the default timestamp behavior.
		/// </summary>
		@default = 0,
		/// <summary>
		/// The value that, when present in the request's flags, specifies that audio information should include word-level
		/// timestamp information.
		/// </summary>
		word = 1,
		/// <summary>
		/// The value that, when present in the request's flags, specifies that audio information should include
		/// segment-level timestamp information.
		/// </summary>
		segment = 2,
	}
}
