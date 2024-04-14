using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Shared.JocysCom;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Cryptography;
using MimeKit.Text;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
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

		/// <summary>
		/// Event for logging.
		/// </summary>
		public event EventHandler<string> LogMessage;

		/// <summary>
		/// Tests the connectivity and authentication with the mail server.
		/// </summary>
		/// <returns>A task that represents the asynchronous operation. The task result contains the connection and authentication status as a string.</returns>
		public async Task TestAccount(bool isImap)
		{
			var ms = new MemoryStream();
			var logger = new ProtocolLogger(ms, true);
			MailService client = isImap
				? (MailService)new ImapClient(logger)
				: new SmtpClient(logger);
			int port = isImap
				? Account.ServerImapPort
				: Account.ServerSmtpPort;

			client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
			{
				bool allow = false;
				// No errors were found.
				if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
				{
					// Allow this client to communicate with unauthenticated servers.
					return true;
				}
				string message = string.Format("Certificate error: {0}", sslPolicyErrors);
				message += allow
					? " Allow this client to communicate with unauthenticated server."
					: " The underlying connection was closed.";
				if (sender != null && sender is System.Net.HttpWebRequest)
				{
					var hr = (System.Net.HttpWebRequest)sender;
					message += $"sender.OriginalString: {hr.Address.OriginalString}";
				}
				if (certificate != null)
				{
					message += $"Certificate.Issuer: {certificate.Issuer}\r\n";
					message += $"Certificate.Subject: {certificate.Subject}\r\n";
				}
				if (chain != null)
				{
					for (int i = 0; i < chain.ChainStatus.Length; i++)
					{
						var status = $"{chain.ChainStatus[i].Status}, {chain.ChainStatus[i].StatusInformation}";
						message += $"Chain.ChainStatus({i}): {status}\r\n";
					}
				}
				OnLogMessage(message);
				return false;
			};
			var security = isImap
				? (SecureSocketOptions)Account.ImapConnectionSecurity
				: (SecureSocketOptions)Account.SmtpConnectionSecurity;
			try
			{
				OnLogMessage("Connecting to mail server...");
				await client.ConnectAsync(Account.ServerHost, port, security);
				if (!client.IsConnected)
				{
					OnLogMessage("Failed to connect to the server.");
				}
				else
				{
					OnLogMessage("Connected. Authenticating...");
					await client.AuthenticateAsync(Account.Username, Account.Password);
					if (!client.IsAuthenticated)
					{
						OnLogMessage("Authentication failed.");
					}
					else
					{
						OnLogMessage("Authenticated successfully.");
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage?.Invoke(this, $"Error: {ex.Message}");
			}
			finally
			{
				if (client.IsConnected)
				{
					client.Disconnect(true);
					OnLogMessage("Disconnected from the server.");
				}
			}
			var log = System.Text.Encoding.UTF8.GetString(ms.ToArray());
			OnLogMessage(log);
			client.Dispose();
		}

		/// <summary>
		/// Helper method to raise the log message event safely.
		/// </summary>
		protected virtual void OnLogMessage(string message)
			=> LogMessage?.Invoke(this, message);


		#region Monitor Email

		/// <summary>
		/// Fired when new message is received.
		/// </summary>
		public event EventHandler<MimeMessage> NewMessage;


		/// <summary>
		/// Subscribe or unsubscribe from mailbox monitoring based on property change
		/// </summary>
		private async void Account_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MailAccount.MonitorInbox))
				await MonitorMailbox(Account.MonitorInbox);
		}

		private CancellationTokenSource _monitoringCancellationTokenSource;

		/// <summary>
		/// Start or stop monitoring the IMAP inbox.
		/// </summary>
		/// <param name="enable">True to start monitoring, false to stop.</param>
		private async Task MonitorMailbox(bool enable)
		{
			if (enable)
			{
				if (_monitoringCancellationTokenSource == null || _monitoringCancellationTokenSource.IsCancellationRequested)
				{
					_monitoringCancellationTokenSource = new CancellationTokenSource();
					await Task.Run(() => StartMonitoring(_monitoringCancellationTokenSource.Token), _monitoringCancellationTokenSource.Token);
				}
				return;
			}
			StopMonitoring();
		}

		private void StopMonitoring()
		{
			if (_monitoringCancellationTokenSource == null)
				return;
			_monitoringCancellationTokenSource.Cancel();
			_monitoringCancellationTokenSource.Dispose();
			_monitoringCancellationTokenSource = null;
		}

		private async Task StartMonitoring(CancellationToken cancellationToken)
		{
			using (var client = new ImapClient())
			{
				try
				{
					await client.ConnectAsync(Account.ServerHost, Account.ServerImapPort, (SecureSocketOptions)Account.ImapConnectionSecurity, cancellationToken);
					await client.AuthenticateAsync(Account.Username, Account.Password, cancellationToken);
					if (!client.IsConnected || !client.IsAuthenticated)
						throw new InvalidOperationException("Failed to connect or authenticate to the mail server.");
					client.Inbox.Open(FolderAccess.ReadOnly, cancellationToken);
					while (!cancellationToken.IsCancellationRequested)
					{
						var uids = await client.Inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
						foreach (var uid in uids)
						{
							var message = await client.Inbox.GetMessageAsync(uid, cancellationToken);
							// Process new message
							OnNewMessageReceived(message);
						}
						// Wait before checking for new messages to avoid constant polling
						await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
					}
				}
				catch (OperationCanceledException)
				{
					// Expected when monitoring is stopped; simply exit the loop
				}
				catch (Exception ex)
				{
					// Log or handle exceptions
					OnLogMessage($"Error during mailbox monitoring: {ex.Message}");
				}
				finally
				{
					if (client.IsConnected)
						client.Disconnect(true, cancellationToken);
				}
			}
		}

		private bool IsValidAddress(string address, string allowedList)
		{
			if (string.IsNullOrWhiteSpace(address))
			{
				OnLogMessage($"Address is empty!");
				return false;
			}
			var allowedSenders = GetLines(allowedList);
			if (!allowedSenders.Any())
			{
				OnLogMessage($"Allowed list is empty!");
				return false;
			}
			if (!allowedSenders.Contains(allowedList, StringComparer.OrdinalIgnoreCase))
			{
				OnLogMessage($"{address} not allowed!");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Verify digital signature.
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		private bool IsValidDigitalSignature(MimeMessage message)
		{
			if (message.Body is MultipartSigned)
			{
				var signed = (MultipartSigned)message.Body;
				using (var ctx = new WindowsSecureMimeContext())
				{
					foreach (var signature in signed.Verify(ctx))
					{
						try
						{
							// If valid is true, then it signifies that the signed content
							// has not been modified since this particular signer signed the
							// content.
							bool valid = signature.Verify();
							OnLogMessage($"Digital Signature is valid: {valid}");
							return valid;
						}
						catch (Exception ex)
						{
							OnLogMessage($"Digital Signature verification failed. {ex.Message}");
							return false;
						}
					}
				}
				OnLogMessage("Digital Signature verification failed.");
				return false;
			}
			return false;
		}

		private bool IsValidDkim(MimeMessage message)
		{
			try
			{
				var index = message.Headers.IndexOf(HeaderId.DkimSignature);
				if (index == -1)
				{
					OnLogMessage("DKIM Signature is missing");
					return false;
				}
				var locator = new DomainPublicKeyLocator();
				var verifier = new DkimVerifier(locator);
				var dkim = message.Headers[index];
				var valid = verifier.Verify(message, dkim);
				OnLogMessage($"DKIM is valid: {valid}");
				return valid;
			}
			catch (Exception ex)
			{
				OnLogMessage($"DKIM verification failed. {ex.Message}");
				return false;
			}
		}

		protected virtual bool IsValidReceivedMessage(MimeMessage message)
		{
			var sender = message.From.Mailboxes.FirstOrDefault()?.Address;
			// Validation.
			if (Account.ValidateSenders && !IsValidAddress(sender, Account.AllowedSenders))
				return false;
			if (Account.ValidateDigitalSignature && !IsValidDigitalSignature(message))
				return false;
			if (Account.ValidateDkim && IsValidDkim(message))
				return false;
			return true;
		}

		protected virtual void OnNewMessageReceived(MimeMessage message)
		{
			if (IsValidReceivedMessage(message))
				NewMessage?.Invoke(this, message);
		}

		private static string[] GetLines(string text)
		{
			var lines = text?
				.Replace("\r\n", "\n")
				.Replace("\r", "\n")
				.Split('\n')
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrEmpty(x))
				.ToArray();
			return lines;
		}

		#endregion

		public OperationResult<bool> Send(
				string[] recipients,
				string subject, string body,
				Plugins.Core.MailTextFormat bodyTextFormat)
		{
			return Helper.RunSynchronously(async ()
				=> await SendAsync(recipients, subject, body, bodyTextFormat));
		}

		/// <summary>
		/// Send message
		/// </summary>
		public async Task<OperationResult<bool>> SendAsync(string[] recipients,
		string subject, string body,
		Plugins.Core.MailTextFormat bodyTextFormat,
		CancellationToken cancellationToken = default)
		{
			var message = new MimeMessage();
			message.From.Add(new MailboxAddress(Account.EmailName, Account.EmailAddress));
			foreach (var recipient in recipients)
			{
				message.To.Add(MailboxAddress.Parse(recipient));
			}
			message.Subject = subject;
			message.Body = new TextPart((TextFormat)bodyTextFormat) { Text = body };
			using (var client = new SmtpClient())
			{
				try
				{
					await client.ConnectAsync(Account.ServerHost, Account.ServerSmtpPort, (SecureSocketOptions)Account.SmtpConnectionSecurity, cancellationToken);
					await client.AuthenticateAsync(Account.Username, Account.Password);
					await client.SendAsync(message);
				}
				catch (Exception ex)
				{
					OnLogMessage($"Error sending email: {ex.Message}");
					return new OperationResult<bool>(ex);
					// Handle exceptions or log them
				}
				finally
				{
					await client.DisconnectAsync(true);
					OnLogMessage("Email sent successfully.");
				}
			}
			return new OperationResult<bool>(true);
		}


	}

}
