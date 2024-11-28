namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>Image info.</summary>
	public class ImageInfo : BasicInfo
	{

		/// <summary>Image info.</summary>
		public ImageInfo()
		{
			Type = ContextType.Image;
		}

		/// <summary>Image width.</summary>
		public int Width { get; set; }

		/// <summary>Image height.</summary>
		public int Height { get; set; }
	}
}
