using Microsoft.CognitiveServices.Speech;
using System;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{

	/// <summary>
	/// Contains location and length details about words in synthesized speech.
	/// </summary>
	public class WordBoundary
	{

		/// <summary>Specifies unique ID of speech synthesis.</summary>
		public string ResultId { get; set; }

		/// <summary>Specifies current word's offset in output audio, in ticks (ms).</summary>
		public int AudioOffset { get; set; }

		/// <summary>Time duration of the audio.</summary>
		public TimeSpan Duration { get; set; }

		/// <summary>Specifies current word's text offset in input text, in characters.</summary>
		public int TextOffset { get; set; }

		/// <summary>Specifies current word's length, in characters.</summary>
		public uint WordLength { get; set; }

		/// <summary>The text.</summary>
		public string Text { get; set; }

		/// <summary>Word boundary type.</summary>
		public SpeechSynthesisBoundaryType BoundaryType { get; set; }

	}
}
