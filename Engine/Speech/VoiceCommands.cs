using System;
using System.Linq;
using System.Speech.Recognition;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	public class VoiceCommands
	{
		private SpeechRecognitionEngine recognizer;
		private bool isListening;

		public event Action<string> CommandRecognized;

		public static (RecognizerInfo, Exception) CheckSpeechRecognizers()
		{
			// Attempt to find any installed recognizer that supports English
			var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
				.FirstOrDefault(ri => ri.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase));
			// If no English recognizer found then...
			if (recognizerInfo == null)
			{
				var message = "No English speech recognizer is installed on this system. Speech recognition will be disabled.";
				System.Diagnostics.Debug.WriteLine(message);
				return (null, new Exception(message));
			}
			return (recognizerInfo, null);
		}

		public VoiceCommands(params string[] voiceCommands)
		{
			// Attempt to find any installed recognizer that supports English
			var (recognizerInfo, exception) = CheckSpeechRecognizers();
			if (exception != null)
				return;

			// Use the culture of the selected recognizer
			var culture = recognizerInfo.Culture;

			recognizer = new SpeechRecognitionEngine(recognizerInfo);

			// Create choices for the words to recognize
			var commands = new Choices();
			if (voiceCommands == null || voiceCommands.Length == 0)
				voiceCommands = new string[] { "accept", "deny" };
			commands.Add(voiceCommands);

			// Create grammar builder and grammar
			var gb = new GrammarBuilder();
			gb.Culture = culture; // Set the culture to match the recognizer
			gb.Append(commands);
			var grammar = new Grammar(gb);

			try
			{
				recognizer.LoadGrammar(grammar);
				recognizer.SetInputToDefaultAudioDevice();

				recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
				recognizer.RecognizeCompleted += Recognizer_RecognizeCompleted;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"An error occurred while initializing speech recognition: {ex.Message}");
				recognizer.Dispose();
				recognizer = null;
			}
		}

		public void StartListening()
		{
			if (isListening || recognizer == null)
				return;
			isListening = true;
			try
			{
				recognizer.RecognizeAsync(RecognizeMode.Multiple);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"An error occurred while starting speech recognition: {ex.Message}");
				isListening = false;
			}
		}

		public void StopListening()
		{
			if (!isListening || recognizer == null)
				return;
			isListening = false;
			try
			{
				recognizer.RecognizeAsyncStop();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"An error occurred while stopping speech recognition: {ex.Message}");
			}
		}

		private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
		{
			var command = e.Result.Text.ToLower();
			CommandRecognized?.Invoke(command);
		}

		private void Recognizer_RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("Recognition completed.");
		}
	}
}
