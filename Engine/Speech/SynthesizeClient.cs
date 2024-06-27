using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
#if NETCOREAPP
using System.Runtime.Versioning;
#endif

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
			synthesizer.WordBoundary += Synthesizer_WordBoundary;
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

		private void Synthesizer_WordBoundary(object sender, SpeechSynthesisWordBoundaryEventArgs e)
		{
			var wb = new WordBoundary()
			{
				AudioOffset = (int)(e.AudioOffset / 10000),
				BoundaryType = e.BoundaryType,
				Duration = e.Duration,
				ResultId = e.ResultId,
				Text = e.Text,
				TextOffset = (int)e.TextOffset,
				WordLength = e.WordLength,
			};
			AudioInfo.Boundaries.Add(wb);
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
			// Update text to SSML XML.
			var updatedText = UpdateToViseme(text, Config.SpeechSynthesisVoiceName);
			useSsml = true;
			var newData = await _Synthesize(updatedText, useSsml, useCache);
			if (newData)
				ClassLibrary.Runtime.Serializer.SerializeToXmlFile(AudioInfo, AudioInfoPath);
		}

		public static string GetOuptuPath() => Path.Combine(Global.AppData.XmlFile.Directory.FullName, "Temp");

		public string ConvertXmlToPlainText(string htmlString)
		{
			var doc = new XmlDocument();
			doc.LoadXml(htmlString);
			return ClassLibrary.Xml.XmlDocHelper.ConvertXmlNodesToText(doc.DocumentElement);
		}

		/// <summary>
		/// Start speaking and animation.
		/// </summary>
		/// <param name="input">The text or SSML XML to be spoken.</param>
		/// <param name="useSsml">Flag indicating whether the text is in SSML format.</param>
		public async Task<bool> _Synthesize(string input, bool? useSsml = null, bool useCache = false)
		{
			var useSsml2 = useSsml.HasValue ? useSsml.Value : input.StartsWith("<");
			var inputForFileName = useSsml2
				? ConvertXmlToPlainText(input)
				: input;
			var relativePath = AudioHelper.GetUniqueFilePath(null, null, Config.SpeechSynthesisVoiceName, "", "", inputForFileName);
			var settings = Global.AppSettings.AiAvatar;
			var path = GetOuptuPath();
			AudioFilePath = Path.Combine(path, relativePath + GetExtension(settings.CacheAudioFormat));
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
			AudioInfo.Text = input;
			AudioInfo.IsSsml = useSsml2;
			isWorking = true;
			SpeechSynthesisResult result = useSsml2
				? await synthesizer.SpeakSsmlAsync(input)
				: await synthesizer.SpeakTextAsync(input);
			if (result.Reason == ResultReason.SynthesizingAudioCompleted)
			{
				AudioInfo.AudioDuration = result.AudioDuration;
				Console.WriteLine($"Speech synthesized for text: \"{input}\"");

				AudioInfo.AudioDuration = result.AudioDuration;
				Console.WriteLine($"Speech synthesized for text: \"{input}\"");
				using (var audioStream = AudioDataStream.FromResult(result))
				{
					if (settings.CacheAudioFormat == AudioFileFormat.WAV)
					{
						await SaveFile(audioStream, AudioFilePath);
					}
					else
					{
						var ms = await GetMemoryStreamWithHeader(audioStream);
						AudioHelper.Convert(ms, AudioFilePath);
					}
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

		public string GetExtension(AudioFileFormat format)
		{
			if (format == AudioFileFormat.ULaw || format == AudioFileFormat.ALaw)
				return "." + format.ToString().ToLower() + ".wav";
			else if (format == AudioFileFormat.MP3)
				return ".mp3";
			else if (format == AudioFileFormat.WAV)
				return ".wav";
			else
				// Do nothing if format do not match.
				return "";
		}

		/// <summary>
		/// Write the audio data to a file
		/// </summary>
		public static async Task SaveFile(AudioDataStream source, string fileName)
		{
			var fi = new FileInfo(fileName);
			// Write the audio data to a file
			if (!fi.Directory.Exists)
				fi.Directory.Create();
			// Save the synthesized speech to a WAV file
			await source.SaveToWaveFileAsync(fileName);
			Console.WriteLine($"Audio content written to file \"{fileName}\"");
		}

		public MemoryStream GetMemoryStream(AudioDataStream source)
		{
			MemoryStream ms = new MemoryStream();
			var buffer = new byte[8000];
			uint bytesRead;
			while ((bytesRead = source.ReadData(buffer)) > 0)
				ms.Write(buffer, 0, (int)bytesRead);
			ms.Position = 0;
			return ms;
		}

		public async Task<MemoryStream> GetMemoryStreamWithHeader(AudioDataStream source)
		{
			var tempWavFilePath = Path.GetRandomFileName() + ".wav";
			// Save to a temporary WAV file first
			await source.SaveToWaveFileAsync(tempWavFilePath);
			var ms = new MemoryStream();
			using (var stream = File.OpenRead(tempWavFilePath))
				stream.CopyTo(ms);
			// Set the position to the beginning of the stream
			ms.Position = 0;
			// Clean up temporary file
			File.Delete(tempWavFilePath);
			return ms;
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

		#region Update to Viseme

		// Define namespaces
		static XNamespace ns = "http://www.w3.org/2001/10/synthesis";
		static XNamespace mstts = "http://www.w3.org/2001/mstts";

		/// <summary>
		/// Update to Speech Synthesis Markup Language Version 1.0 with Viseme and Shapes.
		/// Function adds missing tags and wraps into appropriate elements.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="voiceName"></param>
		/// <returns></returns>
		public static string UpdateToViseme(string input, string voiceName)
		{
			XElement speakElement;
			// Check if input is valid XML
			if (IsXml(input, out List<XElement> elements))
			{
				// If it's already a <speak> element, use it directly
				if (elements.Count == 1 && elements[0].Name.LocalName == "speak")
				{
					speakElement = elements[0];
				}
				else
				{
					// Wrap other valid XML elements with <speak>
					speakElement = new XElement(ns + "speak", elements);
				}
			}
			else
			{
				// Wrap non-XML input with <speak>
				speakElement = new XElement(ns + "speak", new XText(input));
			}
			speakElement = EnsureSpeakElement(speakElement);
			EnsureVoiceElements(speakElement, voiceName);
			return speakElement.ToString();
		}

		/// <summary>
		/// Ensures the <speak> element has necessary attributes and wraps content if needed.
		/// </summary>
		static XElement EnsureSpeakElement(XElement element)
		{
			// Ensure <speak> element
			var speakElement = element.Name == ns + "speak"
				? element
				: new XElement(ns + "speak", element.Nodes());
			// Ensure necessary attributes
			if (!speakElement.Attribute("version")?.Value.Equals("1.0") ?? true)
				speakElement.SetAttributeValue("version", "1.0");
			if (!speakElement.Attribute(XNamespace.Xmlns + "mstts")?.Value.Equals(mstts.NamespaceName) ?? true)
				speakElement.SetAttributeValue(XNamespace.Xmlns + "mstts", mstts.NamespaceName);
			if (!speakElement.Attribute(XNamespace.Xml + "lang")?.Value.Equals("en-US") ?? true)
				speakElement.SetAttributeValue(XNamespace.Xml + "lang", "en-US");
			// Fix child elements namespaces.
			foreach (var descendant in speakElement.DescendantsAndSelf())
				if (descendant.Name.Namespace == XNamespace.None)
					descendant.Name = ns + descendant.Name.LocalName;
			return speakElement;
		}
		/// <summary>
		/// Ensures all <voice> elements have necessary attributes and viseme elements.
		/// </summary>
		static void EnsureVoiceElements(XElement container, string voiceName)
		{
			// If no <voice> element, wrap content in <voice> element
			if (!container.Descendants(ns + "voice").Any())
			{
				var content = container.Nodes().ToList();
				container.RemoveNodes();
				container.Add(CreateVoiceElement(content, voiceName));
			}
			// Ensure attributes and viseme elements in <voice> elements
			foreach (var voiceEl in container.Descendants(ns + "voice"))
			{
				if (voiceEl.Attribute("name") == null)
					voiceEl.SetAttributeValue("name", voiceName);
				if (!(voiceEl.Nodes().FirstOrDefault() is XElement firstChild) || firstChild.Name != mstts + "viseme")
					voiceEl.AddFirst(new XElement(mstts + "viseme", new XAttribute("type", "FacialExpression")));
			}
		}

		/// <summary>
		/// Creates a <voice> element with viseme.
		/// </summary>
		static XElement CreateVoiceElement(IEnumerable<XNode> content, string voiceName)
		{
			var voiceElement = new XElement(ns + "voice", new XAttribute("name", voiceName));
			voiceElement.Add(new XElement(mstts + "viseme", new XAttribute("type", "FacialExpression")));
			voiceElement.Add(content);
			return voiceElement;
		}

		/// <summary>
		/// Determines if the input text is well-formed XML, and captures elements.
		/// </summary>
		private static bool IsXml(string text, out List<XElement> elements)
		{
			elements = new List<XElement>();
			try
			{
				// Attempt to parse text as XML
				var xmlText = $"<root>{text}</root>";
				var rootElement = XElement.Parse(xmlText);
				// Extract child elements, ignoring the artificial root
				elements = rootElement.Elements().ToList();
				return elements.Count > 0;
			}
			catch
			{
				// If parsing fails, it's not valid XML
				elements = null;
				return false;
			}
		}


		#endregion


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
				var s = synthesizer;
				if (s != null)
				{
					s.VisemeReceived -= Synthesizer_VisemeReceived;
					s.WordBoundary -= Synthesizer_WordBoundary;
					s.Dispose();
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
