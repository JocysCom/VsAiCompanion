using NAudio.MediaFoundation;
using NAudio.Wave;
using System.IO;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	public class AudioHelper
	{

		/// <summary>
		/// Get unique file name to save by content.
		/// </summary>
		public static string GetUniqueFilePath(
			string app,
			string groupName, string voiceName, string gender, string effect,
			string text)
		{
			if (!string.IsNullOrEmpty(groupName))
				groupName = ClassLibrary.Text.Filters.GetKey(groupName, false);
			string fileName;
			var encoding = System.Text.Encoding.UTF8;
			var charPath = ClassLibrary.Text.Filters.GetKey(string.Format("{0}_{1}_{2}", voiceName, gender, effect ?? ""), false);
			// Generalize text if needed.
			fileName = ClassLibrary.Text.Filters.GetKey(text, false);
			// If file name will be short then...
			var maxLen = 48;
			if (fileName.Length >= maxLen)
			{
				var bytes = encoding.GetBytes(fileName);
				var algorithm = System.Security.Cryptography.SHA256.Create();
				var hash = string.Join("", algorithm.ComputeHash(bytes).Take(8).Select(x => x.ToString("X2")));
				// Return trimmed name with hash.
				fileName = string.Format("{0}_{1}", fileName.Substring(0, maxLen), hash);
			}
			var names = new string[] { app, groupName, charPath, fileName }.Where(x => !string.IsNullOrEmpty(x));
			var relatvePath = string.Join("\\", names);
			return relatvePath;
		}

		public static void Convert(Stream source, string fullName)
		{
			var fi = new FileInfo(fullName);
			// Create directory if not exists.
			if (!fi.Directory.Exists)
				fi.Directory.Create();
			// https://www.codeproject.com/Articles/501521/How-to-convert-between-most-audio-formats-in-NET
			source.Position = 0;
			var settings = Global.AppSettings.AiAvatar;
			var convertFormat = Global.AppSettings.AiAvatar.CacheAudioFormat;
			var reader = new WaveFileReader(source);
			if (convertFormat == AudioFileFormat.ULaw || convertFormat == AudioFileFormat.ALaw)
			{
				// The ACM mu-law encoder expects its input to be 16 bit.
				// If you're working with mu or a-law, the sample rate is likely to be low as well.
				// The following two lines of code will create a zero-length stream of PCM 16 bit and
				// pass it into a WaveFormatConversionStream to convert it to a-law.
				// It should not throw a "conversion not possible" error unless for some reason you don't have the G.711 encoder installed on your machine.
				var wavFormat = convertFormat == AudioFileFormat.ULaw
						? WaveFormatEncoding.MuLaw
						: WaveFormatEncoding.ALaw;

				var destinationFormat = WaveFormat.CreateCustomFormat(
					wavFormat,
					settings.CacheAudioSampleRate,
					(int)settings.CacheAudioChannels,
					settings.CacheAudioAverageBitsPerSecond / 8,
					settings.CacheAudioBlockAlign,
					settings.CacheAudioBitsPerSample
				);
				var conversionStream1 = new WaveFormatConversionStream(new WaveFormat(destinationFormat.SampleRate, 16, destinationFormat.Channels), reader);
				using (var conversionStream2 = new WaveFormatConversionStream(destinationFormat, conversionStream1))
					WaveFileWriter.CreateWaveFile(fullName, conversionStream2);
				conversionStream1.Dispose();
			}
			else if (convertFormat == AudioFileFormat.MP3)
			{
				// If media foundation is not started then you will get this exception during MP3 encoding:
				// System.Runtime.InteropServices.COMException:
				// 'The request is invalid because Shutdown() has been called. (Exception from HRESULT: 0xC00D3E85)'
				MediaFoundationApi.Startup();
				MediaFoundationEncoder.EncodeToMp3(reader, fullName, settings.CacheAudioAverageBitsPerSecond);
				MediaFoundationApi.Shutdown();
			}
			else if (convertFormat == AudioFileFormat.WAV)
			{
				var destWav = new WaveFormat(
					settings.CacheAudioSampleRate,
					settings.CacheAudioBitsPerSample,
					(int)settings.CacheAudioChannels
				);
				using (var conversionStream2 = new WaveFormatConversionStream(destWav, reader))
					WaveFileWriter.CreateWaveFile(fullName, conversionStream2);
			}
			reader.Dispose();
		}

	}
}
