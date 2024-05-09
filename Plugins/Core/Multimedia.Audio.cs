using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Plugins.Core.TtsMonitor;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// TTS functions, in combination with Jocys.com TTS Monitor app (Jocys.com/TTS), allow AI to speak and narrate stories or dialogues using text-to-speech voices. Works well with Windows Voice Typing. Enable text-to-speech:
	/// AI Companion > Enable all TTS plugins in this tab. Add message instruction for AI like "Answer with text-to-speech voice." 
	/// TTS Monitor > [Options] tab > [Monitor: Server] tab > check "Enable" checkbox. 
	/// TTS Monitor > [Options] tab > [Monitor: Display] tab > uncheck "Enable" checkbox.
	/// </summary>
	public partial class Multimedia
	{

		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<string, bool, Task<OperationResult<string>>> AISpeakCallback { get; set; }

		/// <summary>
		/// Triggers the AI Avatar to articulate the provided text through speech synthesis. 
		/// This function must be used by the AI whenever it is required to speak or respond to user queries.
		/// This direct association with the AI's speech mechanism ensures that all verbal outputs adhere to a consistent and controlled format.
		/// </summary>
		/// <param name="text">String that the AI is expected to vocalize.</param>
		// The input text should be in SSML format to offer enhanced control over the speech characteristics like tone, pitch, and pacing, enabling more human-like and expressive responses.
		// <param name="isSsml">Text format is the SSML-formatted string.</param>
		[RiskLevel(RiskLevel.None)]
		public async Task<OperationResult<string>> AISpeak(string text)
		{
			return await AISpeakCallback(text, false);
		}

		/// <summary>
		/// Start playing text. This could be used for speaking with the user or narrating books or stories.
		/// </summary>
		/// <param name="name">Character name.</param>
		/// <param name="text">Text to play.</param>
		/// <param name="gender">Character gender: 'Male', 'Female', 'Neutral'. Default: 'Male'.</param>
		/// <param name="language">Language Culture. Use Language[-Location] format. For example: 'en-GB'. Default is set by the user.</param>
		/// <param name="effect">Sound effects:
		/// 'Default', 'Beast', 'Demon', 'Dragonkin', 'Elemental',
		/// 'Giant', 'Humanoid', 'Mechanical', 'Undead'. Default is 'Default' for humans.
		/// </param>
		/// <param name="group">Message chat group: 'Quest'. Default: 'Quest'.</param>
		/// <param name="pitch">Voice pitch: Range from -10 to 10. Default is set by the user.</param>
		/// <param name="rate">Voice rate or speed: Range from -10 to 10.  Default is set by the user.</param>
		/// <param name="volume">Voice volume. Range from 0 to 100.  Default is set by the user.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool PlayText(
				string name,
				string text = null,
				VoiceGender? gender = null,
				string language = null,
				string effect = null,
				string group = null,
				int? pitch = null,
				int? rate = null,
				int? volume = null
				)

		{
			var message = new message();
			message.command = "save";
			message.name = name;
			message.gender = gender?.ToString();
			message.language = language;
			message.effect = effect;
			message.group = group;
			message.rate = rate?.ToString();
			message.pitch = pitch?.ToString();
			message.volume = volume?.ToString();
			SendMessage(message);

			message.command = "play";
			//message.effect = null;
			message.part = text;
			return SendMessage(message);
		}

		/// <summary>
		/// Stop text-to-speech playback.
		/// </summary>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool StopText()
		{
			var message = new message();
			message.command = "stop";
			return SendMessage(message);
		}

		private bool SendMessage(message message)
		{
			try
			{
				var messageXml = JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlString(message, omitXmlDeclaration: true);
				// C# example how to send message to Text to Speech Monitor from another program.
				var clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				var address = IPAddress.Parse("127.0.0.1");
				var remoteEP = new IPEndPoint(address, 42500);
				clientSock.Connect(remoteEP);
				var bytes = System.Text.Encoding.UTF8.GetBytes(messageXml);
				clientSock.Send(bytes);
				clientSock.Close();
				return true;
			}
			catch (System.Exception)
			{
				return false;
			}
		}

	}
}
