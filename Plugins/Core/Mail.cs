using JocysCom.VS.AiCompanion.Shared.JocysCom;
using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Allows AI to check and send mail.
	/// </summary>
	public class Mail
	{

		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<string[], string, string, MailTextFormat, OperationResult<bool>> SendCallback { get; set; }

		/// <summary>
		/// Send mail file text content on user computer.
		/// </summary>
		/// <param name="recipients">Email address of recipients.</param>
		/// <param name="subject">Email subject.</param>
		/// <param name="body">Email body.</param>
		/// <param name="bodyTextFormat">Email body text format.</param>
		[RiskLevel(RiskLevel.High)]
		public OperationResult<bool> Send(
			string[] recipients,
			string subject, string body,
			MailTextFormat bodyTextFormat)
		{
			return SendCallback(recipients, subject, body, bodyTextFormat);
		}
	}
}
