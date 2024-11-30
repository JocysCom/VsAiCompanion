namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary> The format of the transcription. </summary>
	public enum audio_transcription_format
	{
		/// <summary> Plain text only. </summary>
		Text,
		/// <summary> Plain text only. </summary>
		Json,
		/// <summary> Plain text provided with additional metadata, such as duration and timestamps. </summary>
		VerboseJson,
		/// <summary> Text formatted as SubRip (.srt) file. </summary>
		Srt,
		/// <summary> Text formatted as a Web Video Text Tracks, a.k.a. WebVTT, (.vtt) file. </summary>
		Vtt,
	}
}
