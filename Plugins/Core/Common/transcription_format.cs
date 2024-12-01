namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary> The format of the transcription. </summary>
	public enum audio_transcription_format
	{
		/// <summary> Plain text only. </summary>
		text,
		/// <summary> Plain text only. </summary>
		json,
		/// <summary> Plain text provided with additional metadata, such as duration and timestamps. </summary>
		verbose_json,
		/// <summary> Text formatted as SubRip (.srt) file. </summary>
		srt,
		/// <summary> Text formatted as a Web Video Text Tracks, a.k.a. WebVTT, (.vtt) file. </summary>
		vtt,
	}

}
