using JocysCom.ClassLibrary.Web.Services;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using System;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Main client class for ChatGPT AI service interaction.
	/// Implements IAiClient interface for AI service communication.
	/// </summary>
	public partial class Client : IAiClient
	{
		#region Constructor and Constants

		/// <summary>
		/// Initializes a new instance of the Client class with the specified AI service.
		/// </summary>
		/// <param name="service">The AI service configuration to use for this client.</param>
		public Client(AiService service)
		{
			Service = service;
		}

		private const string usagePath = "usage";
		private const string modelsPath = "models";
		private const string filesPath = "files";
		private const string fineTuningJobsPath = "fine_tuning/jobs";
		public const string FineTuningPurpose = "fine-tune";

		#endregion

		#region Properties

		/// <summary>
		/// The AI service configuration used by this client.
		/// </summary>
		private readonly AiService Service;

		/// <summary>
		/// Gets the HTTP client logger for monitoring requests and responses.
		/// Can be used to log response and reply.
		/// </summary>
		public HttpClientLogger Logger => _Logger;
		HttpClientLogger _Logger;

		/// <summary>
		/// Gets or sets the last error message encountered during API operations.
		/// </summary>
		public string LastError { get; set; }

		/// <summary>
		/// Event raised when a message processing is completed.
		/// </summary>
		public event EventHandler MessageDone;

		#endregion
	}
}
