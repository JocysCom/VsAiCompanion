namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Image data.
	/// </summary>
	public class ImageInfo
	{
		/// <summary>Image relative path</summary>
		public string Path { get; set; }

		/// <summary>Image full path</summary>
		public string FullPath { get; set; }
		/// <summary>AI Prompt used to generate this image.</summary>

		public string Prompt { get; set; }
		/// <summary>Image width.</summary>
		public int Width { get; set; }
		/// <summary>Image height.</summary>
		public int Height { get; set; }
	}
}
