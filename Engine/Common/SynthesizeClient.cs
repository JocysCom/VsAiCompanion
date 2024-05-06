using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Microsoft Viseme Client for synthesizing speech and controlling avatar animations.
	/// </summary>
	public class SynthesizeClient : IDisposable
	{
		private SpeechSynthesizer synthesizer;
		public SpeechConfig Config { get; }
		SoundPlayer player = new SoundPlayer();
		private bool isSpeaking = false;

		public Dictionary<int, int> CurrentViseme = new Dictionary<int, int>();
		public string CurrentAudioFile;

		public SynthesizeClient(string subscriptionKey, string serviceRegion, string voiceName = null)
		{
			var config = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);
			if (!string.IsNullOrEmpty(voiceName))
			{
				// Set the voice name on the config if it's provided
				config.SpeechSynthesisVoiceName = voiceName;
			}
			Config = config;
			synthesizer = new SpeechSynthesizer(config, null);
			// Subscribes to viseme received event for animation control
			synthesizer.VisemeReceived += Synthesizer_VisemeReceived;
		}

		public event EventHandler<SpeechSynthesisVisemeEventArgs> VisemeReceived;

		private void Synthesizer_VisemeReceived(object sender, SpeechSynthesisVisemeEventArgs e)
		{
			CurrentViseme.Add((int)(e.AudioOffset / 10000), (int)e.VisemeId);
			VisemeReceived?.Invoke(this, e);
			Console.WriteLine($"Viseme event received. Audio offset: {e.AudioOffset / 10000}ms, viseme id: {e.VisemeId}.");
			//AnimateAvatarBasedOnViseme(e.VisemeId);
		}

		private void AnimateAvatarBasedOnViseme(int visemeId)
		{
			// This is where you implement animation logic.
			// For demonstration purposes, let's just log the viseme ID.
			Console.WriteLine($"Animating avatar with viseme: {visemeId}");

			// Example: Switch or if statements mapping visemeId to animation actions.
		}

		/// <summary>
		/// Start speaking and animation.
		/// </summary>
		/// <param name="text">The text to be spoken.</param>
		/// <param name="useSsml">Flag indicating whether the text is in SSML format.</param>
		public async Task Play(string text, bool useSsml = false)
		{
			CurrentViseme.Clear();
			CurrentAudioFile = null;
			isSpeaking = true;
			SpeechSynthesisResult result = useSsml
				? await synthesizer.SpeakSsmlAsync(text)
				: await synthesizer.SpeakTextAsync(text);

			if (result.Reason == ResultReason.SynthesizingAudioCompleted)
			{
				Console.WriteLine($"Speech synthesized for text: \"{text}\"");
				// Write the audio data to a file
				var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, "Temp");
				var relativePath = AudioHelper.GetUniqueFilePath(null, null, Config.SpeechSynthesisVoiceName, "", "", text);
				var outputPath = Path.Combine(path, relativePath + ".wav");
				var fi = new FileInfo(outputPath);
				if (!fi.Directory.Exists)
					fi.Directory.Create();
				using (var audioStream = AudioDataStream.FromResult(result))
				{
					// Save the synthesized speech to a WAV file
					await audioStream.SaveToWaveFileAsync(outputPath);
					Console.WriteLine($"Audio content written to file \"{outputPath}\"");
				}
				CurrentAudioFile = outputPath;
			}
			else if (result.Reason == ResultReason.Canceled)
			{
				var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
				Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");
				Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
				Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
			}
			isSpeaking = false;
		}

		public async Task<List<string>> GetAvailableVoicesAsync()
		{
			var result = await synthesizer.GetVoicesAsync();
			List<string> voiceNames = new List<string>();
			foreach (var voice in result.Voices)
				voiceNames.Add(voice.Name);
			return voiceNames;
		}

		public void PlayFile(string path)
		{
			// Play the audio file
			SoundPlayer player = new SoundPlayer(path);
			player.Play(); // Plays the audio asynchronously
			Console.WriteLine("Playing audio...");
		}

		/// <summary>
		/// Stop speaking and animation.
		/// </summary>
		public async Task Stop()
		{
			if (isSpeaking)
			{
				await synthesizer.StopSpeakingAsync();
				player.Stop();
				Console.WriteLine("Speech and animation stopped.");
			}
		}

		public void Dispose()
		{
			synthesizer?.Dispose();
		}
	}
}
