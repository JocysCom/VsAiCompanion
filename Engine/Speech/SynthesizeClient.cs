using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;

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

		public async Task Synthesize(string text, bool? useSsml = false, bool useCache = false)
		{
			var updatedText = UpdateToViseme(text, Config.SpeechSynthesisVoiceName);
			var newData = await _Synthesize(updatedText, useSsml, useCache);
			if (newData)
				ClassLibrary.Runtime.Serializer.SerializeToXmlFile(AudioInfo, AudioInfoPath);
		}

		/// <summary>
		/// Start speaking and animation.
		/// </summary>
		/// <param name="text">The text to be spoken.</param>
		/// <param name="useSsml">Flag indicating whether the text is in SSML format.</param>
		public async Task<bool> _Synthesize(string text, bool? useSsml = null, bool useCache = false)
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
			var useSsml2 = useSsml.HasValue ? useSsml.Value : text.StartsWith("<");
			AudioInfo = new AudioFileInfo();
			AudioInfo.Text = text;
			AudioInfo.IsSsml = useSsml2;
			isWorking = true;
			SpeechSynthesisResult result = useSsml2
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
		public async Task<List<VoiceItem>> GetAvailableVoicesWithDetailsAsync()
		{
			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.SubscriptionKey);
				string url = $"https://{Config.Region}.tts.speech.microsoft.com/cognitiveservices/voices/list";
				var response = await client.GetAsync(url);
				response.EnsureSuccessStatusCode();
				string responseBody = await response.Content.ReadAsStringAsync();
				List<VoiceItem> voiceDetails = System.Text.Json.JsonSerializer.Deserialize<List<VoiceItem>>(responseBody);
				return voiceDetails;
			}
		}

		/// <summary>
		/// Update to Speech Synthesis Markup Language Version 1.0 with Viseme and Shapes.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="voiceName"></param>
		/// <returns></returns>
		public static string UpdateToViseme(string text, string voiceName)
		{
			// Define namespaces
			XNamespace ns = "http://www.w3.org/2001/10/synthesis";
			XNamespace mstts = "http://www.w3.org/2001/mstts";
			// Create the root element <speak> with necessary attributes
			XElement speakElement = new XElement(ns + "speak",
				new XAttribute("version", "1.0"),
				new XAttribute(XNamespace.Xmlns + "mstts", mstts.NamespaceName),
				new XAttribute(XNamespace.Xml + "lang", "en-US"));
			// Create the <voice> element with the provided voiceName
			XElement voiceElement = new XElement(ns + "voice",
				new XAttribute("name", voiceName));
			// Add <mstts:viseme> element
			XElement visemeElement = new XElement(mstts + "viseme",
				new XAttribute("type", "FacialExpression"));
			// Check if text is plain text or XML
			if (IsXml(text))
			{
				try
				{
					// Parse the XML content and extract inner text
					XElement textElement = XElement.Parse(text);
					voiceElement.Add(visemeElement);
					voiceElement.Add(textElement.Nodes());
				}
				catch (Exception)
				{
					// In case of parsing error, treat as plain text
					voiceElement.Add(visemeElement);
					voiceElement.Add(text);
				}
			}
			else
			{
				// If plain text, just add the text
				voiceElement.Add(visemeElement);
				voiceElement.Add(text);
			}
			// Construct the final XML
			speakElement.Add(voiceElement);
			return speakElement.ToString();
		}

		private static bool IsXml(string text)
		{
			try
			{
				// Attempt to parse text as XML
				XElement.Parse(text);
				return true;
			}
			catch
			{
				// If parsing fails, it's not valid XML
				return false;
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
			try
			{
				synthesizer?.Dispose();
			}
			catch (Exception)
			{
			}
		}
	}
}
