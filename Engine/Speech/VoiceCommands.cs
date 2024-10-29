using System;
using System.Speech.Recognition;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	public class VoiceCommands
	{
		private SpeechRecognitionEngine recognizer;
		private bool isListening;

		public event Action<string> CommandRecognized;

		public VoiceCommands(params string[] voiceCommands)
		{
			recognizer = new SpeechRecognitionEngine();

			// Create choices for the words to recognize
			Choices commands = new Choices();
			if (voiceCommands.Length == 0)
				voiceCommands = new string[] { "accept", "deny" };
			commands.Add(voiceCommands);

			// Create grammar builder and grammar
			GrammarBuilder gb = new GrammarBuilder();
			gb.Append(commands);
			Grammar grammar = new Grammar(gb);

			recognizer.LoadGrammar(grammar);
			recognizer.SetInputToDefaultAudioDevice();

			recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
			recognizer.RecognizeCompleted += Recognizer_RecognizeCompleted;
		}

		public void StartListening()
		{
			if (isListening)
				return;
			isListening = true;
			recognizer.RecognizeAsync(RecognizeMode.Multiple);
		}

		public void StopListening()
		{
			if (!isListening)
				return;
			isListening = false;
			recognizer.RecognizeAsyncStop();
		}

		private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
		{
			var command = e.Result.Text.ToLower();
			CommandRecognized?.Invoke(command);
		}

		private void Recognizer_RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
		{
			Console.WriteLine("Recognition completed.");
		}
	}
}
