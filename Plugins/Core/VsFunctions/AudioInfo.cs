namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>Audio info.</summary>
	public class AudioInfo : BasicInfo
	{
		/// <summary>Voice</summary>
		public string Voice { get; set; }

		/// <summary>Audio info.</summary>
		public AudioInfo()
		{
			Type = ContextType.Audio;
		}

	}
}
