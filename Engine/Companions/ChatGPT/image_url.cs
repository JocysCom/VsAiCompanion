namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class image_url : base_item
	{
		/// <summary>
		/// Either a URL of the image or the base64 encoded image data.
		/// </summary>
		public string url { get; set; }

		/// <summary>
		/// Specifies the detail level of the image. Learn more in the [Vision guide](/docs/guides/vision/low-or-high-fidelity-image-understanding).
		/// </summary>
		public image_url_detail detail { get; set; } = image_url_detail.auto;
	}
}
