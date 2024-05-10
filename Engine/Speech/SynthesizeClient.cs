using JocysCom.VS.AiCompanion.Engine.Audio;
using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	/// <summary>
	/// Microsoft Viseme Client for synthesizing speech and controlling avatar animations.
	/// </summary>
	public class SynthesizeClient : IDisposable
	{
		private SpeechSynthesizer synthesizer;
		public SpeechConfig Config { get; }
		SoundPlayer player;
		private bool isWorking = false;

		public AudioFileInfo AudioInfo;
		public string AudioFilePath;
		public string AudioInfoPath;


#if NETCOREAPP
		[SupportedOSPlatform("windows")]
#endif
		public SynthesizeClient(string subscriptionKey, string serviceRegion, string voiceName = null)
		{
			player = new SoundPlayer();
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



		private void Synthesizer_VisemeReceived(object sender, SpeechSynthesisVisemeEventArgs e)
		{
			if (e.AudioOffset > 0)
				AudioInfo.Viseme.Add(new VisemeItem((int)(e.AudioOffset / 10000), (int)e.VisemeId));
			else if (!string.IsNullOrEmpty(e.Animation))
			{
				var shape = System.Text.Json.JsonSerializer.Deserialize<BlendShape>(e.Animation);
				AudioInfo.Shapes.Add(shape);
			}
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

		public async Task Synthesize(string text, bool useSsml = false, bool useCache = false)
		{
			var newData = await _Synthesize(text, useSsml, useCache);
			if (newData)
				ClassLibrary.Runtime.Serializer.SerializeToXmlFile(AudioInfo, AudioInfoPath);
		}

		/// <summary>
		/// Start speaking and animation.
		/// </summary>
		/// <param name="text">The text to be spoken.</param>
		/// <param name="useSsml">Flag indicating whether the text is in SSML format.</param>
		public async Task<bool> _Synthesize(string text, bool useSsml = false, bool useCache = false)
		{
			var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, "Temp");
			var relativePath = AudioHelper.GetUniqueFilePath(null, null, Config.SpeechSynthesisVoiceName, "", "", text);
			AudioFilePath = Path.Combine(path, relativePath + ".wav");
			AudioInfoPath = Path.Combine(path, relativePath + ".xml");
			var wavFi = new FileInfo(AudioFilePath);
			var xmlFi = new FileInfo(AudioInfoPath);
			if (useCache && wavFi.Exists && xmlFi.Exists)
			{
				try
				{
					AudioInfo = ClassLibrary.Runtime.Serializer.DeserializeFromXmlFile<AudioFileInfo>(AudioInfoPath);
					return false;
				}
				catch { }
			}
			AudioInfo = new AudioFileInfo();
			AudioInfo.Text = text;
			AudioInfo.IsSsml = useSsml;
			isWorking = true;
			SpeechSynthesisResult result = useSsml || text.StartsWith("<")
				? await synthesizer.SpeakSsmlAsync(text)
				: await synthesizer.SpeakTextAsync(text);
			if (result.Reason == ResultReason.SynthesizingAudioCompleted)
			{
				AudioInfo.AudioDuration = result.AudioDuration;
				Console.WriteLine($"Speech synthesized for text: \"{text}\"");
				// Write the audio data to a file
				if (!wavFi.Directory.Exists)
					wavFi.Directory.Create();
				using (var audioStream = AudioDataStream.FromResult(result))
				{
					// Save the synthesized speech to a WAV file
					await audioStream.SaveToWaveFileAsync(AudioFilePath);
					Console.WriteLine($"Audio content written to file \"{AudioFilePath}\"");
				}
			}
			else if (result.Reason == ResultReason.Canceled)
			{
				var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
				Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");
				Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
				Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
			}
			isWorking = false;
			return result.Reason == ResultReason.SynthesizingAudioCompleted;
		}

		public async Task<List<string>> GetAvailableVoicesAsync()
		{
			var result = await synthesizer.GetVoicesAsync();
			List<string> voiceNames = new List<string>();
			foreach (var voice in result.Voices)
				voiceNames.Add(voice.Name);
			return voiceNames;
		}


		// Method to get detailed information about available voices.
		public async Task<List<VoiceProperties>> GetAvailableVoicesWithDetailsAsync()
		{
			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.SubscriptionKey);
				string url = $"https://{Config.Region}.tts.speech.microsoft.com/cognitiveservices/voices/list";
				var response = await client.GetAsync(url);
				response.EnsureSuccessStatusCode();
				string responseBody = await response.Content.ReadAsStringAsync();
				List<VoiceProperties> voiceDetails = System.Text.Json.JsonSerializer.Deserialize<List<VoiceProperties>>(responseBody);
				return voiceDetails;
			}
		}


#if NETCOREAPP
		[SupportedOSPlatform("windows")]
#endif
		public void PlayFile(string path)
		{
			// Play the audio file
			player.SoundLocation = path;
			player.Play(); // Plays the audio asynchronously
			Console.WriteLine("Playing audio...");
		}

		/// <summary>
		/// Stop speaking and animation.
		/// </summary>
#if NETCOREAPP
		[SupportedOSPlatform("windows")]
#endif
		public async Task Stop()
		{
			if (isWorking)
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
