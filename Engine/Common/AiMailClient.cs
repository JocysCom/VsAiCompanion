using MailKit.Net.Imap;
using MailKit.Security;
using System;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiMailClient
	{
		public MailAccount Account { get; set; }

		public AiMailClient(MailAccount account)
		{
			Account = account;
		}

		// Define an event for logging.
		public event EventHandler<string> LogMessage;

		/// <summary>
		/// Tests the connectivity and authentication with the mail server.
		/// </summary>
		/// <returns>A task that represents the asynchronous operation. The task result contains the connection and authentication status as a string.</returns>
		public async Task<string> TestAccount()
		{
			using (var client = new ImapClient())
			{
				try
				{
					LogMessage?.Invoke(this, "Connecting to mail server...");

					// Use SecureSocketOptions.StartTls when the server supports STARTTLS (usually on port 143), or SecureSocketOptions.SslOnConnect for SSL/TLS connection (usually on port 993).
					await client.ConnectAsync(Account.ServerHost, Account.ServerPort, SecureSocketOptions.StartTls);
					if (!client.IsConnected)
						return "Failed to connect to the server.";
					LogMessage?.Invoke(this, "Connected. Authenticating...");
					await client.AuthenticateAsync(Account.Username, Account.Password);
					if (!client.IsAuthenticated)
						return "Authentication failed.";
					LogMessage?.Invoke(this, "Authenticated successfully.");
				}
				catch (Exception ex)
				{
					LogMessage?.Invoke(this, $"Error: {ex.Message}");
					return $"Exception occurred: {ex.Message}";
				}
				finally
				{
					if (client.IsConnected)
					{
						client.Disconnect(true);
						LogMessage?.Invoke(this, "Disconnected from the server.");
					}
				}

				return "Test completed successfully.";
			}
		}

		// Helper method to raise the log message event safely.
		protected virtual void OnLogMessage(string message)
			=> LogMessage?.Invoke(this, message);
	}
}
