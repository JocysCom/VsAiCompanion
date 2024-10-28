namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public enum AttachmentSendType
	{
		/// <summary>
		/// Normal attachment.
		/// </summary>
		None = 0,
		/// <summary>
		/// Attachment is temporary and will be send once.
		/// </summary>
		Temp = 1,
		/// <summary>
		/// Attachment is for user only and won't be sent to the server.
		/// </summary>
		User = 2,
	}
}
