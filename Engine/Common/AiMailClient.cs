using JocysCom.VS.AiCompanion.Shared.JocysCom;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Cryptography;
using MimeKit.Text;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiMailClient
	{

		/// <summary>
		/// Mail account configuration.
		/// </summary>
		public MailAccount Account { get; set; }

		#region Testing

		/// <summary>
		/// Event for logging.
		/// </summary>
		public event EventHandler<string> LogMessage;

		/// <summary>
		/// Tests the connectivity and authentication with the mail server.
		/// </summary>
		/// <returns>A task that represents the asynchronous operation. The task result contains the connection and authentication status as a string.</returns>
		public async Task TestAccount(bool isImap, CancellationToken cancellationToken = default)
		{
			var ms = new MemoryStream();
			var logger = new ProtocolLogger(ms, true);
			MailService client = isImap
				? (MailService)new ImapClient(logger)
				: (MailService)new SmtpClient(logger);
			try
			{
				if (await ConnectAsync(client, cancellationToken))
				{
					if (await AuthenticateAsync(client, cancellationToken))
					{

					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when monitoring is stopped; simply exit the loop
			}
			catch (Exception ex)
			{
				LogMessage?.Invoke(this, $"Error: {ex.Message}");
			}
			finally
			{
				await DiconnectAsync(client, cancellationToken);
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

		#endregion

		#region Monitor Email

		/// <summary>
		/// Fired when new message is received.
		/// </summary>
		public event EventHandler<MimeMessage> NewMessage;

		private CancellationTokenSource _monitoringCancellationTokenSource;

		/// <summary>
		/// Start or stop monitoring the IMAP inbox.
		/// </summary>
		/// <param name="enable">True to start monitoring, false to stop.</param>
		public async Task MonitorMailbox(bool enable)
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

		protected virtual void OnNewMessageReceived(MimeMessage message)
		{
			if (IsValidReceivedMessage(message))
				NewMessage?.Invoke(this, message);
		}

		public async Task StartMonitoring(CancellationToken cancellationToken)
		{
			var client = new ImapClient();
			try
			{
				if (await ConnectAsync(client, cancellationToken))
				{
					if (await AuthenticateAsync(client, cancellationToken))
					{
						await client.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
						// Check if the server supports IDLE command.
						if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
						{
							client.Inbox.CountChanged += Inbox_CountChanged;
							// The client will idle until cancelled.
							await client.IdleAsync(cancellationToken);
						}
						else
						{
							OnLogMessage("IMAP server does not support IDLE. Falling back to polling.");
							await client.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
							while (!cancellationToken.IsCancellationRequested)
							{
								// Temp workaround.
								if (Global.MainControl == null)
									continue;
								// Fetch all messages but only their UIDs and FLAGS
								var allMessages = await client.Inbox.FetchAsync(0, -1, MessageSummaryItems.Flags | MessageSummaryItems.UniqueId, cancellationToken);
								// Filter for messages that are not seen
								var summaries = allMessages.Where(x => !x.Flags.Value.HasFlag(MessageFlags.Seen)).ToList();
								foreach (var summary in summaries)
								{
									// You can now fetch each message individually if needed
									var message = await client.Inbox.GetMessageAsync(summary.UniqueId, cancellationToken);
									OnNewMessageReceived(message);
									// After processing, mark the message as seen
									await client.Inbox.AddFlagsAsync(summary.UniqueId, MessageFlags.Seen, true, cancellationToken);
									// One message at the time.
									break;
								}
								// Wait before checking for new messages to avoid constant polling.
								await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
							}
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when monitoring is stopped; simply exit the function
			}
			catch (Exception ex)
			{
				// Log or handle exceptions
				OnLogMessage($"Error during subscription for new email: {ex.Message}");
			}
			finally
			{
				await DiconnectAsync(client, cancellationToken);
			}
		}

		private void Inbox_CountChanged(object sender, EventArgs e)
		{
			var inbox = sender as ImapFolder;
			var newMessageSummary = inbox.Fetch(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope);

			foreach (var summary in newMessageSummary)
			{
				// Process new message
				var message = inbox.GetMessage(summary.UniqueId);
				OnNewMessageReceived(message);
			}
		}

		#endregion

		#region Send Mail

		public async Task<OperationResult<bool>> Send(
				string[] recipients,
				string subject, string body,
				Plugins.Core.MailTextFormat bodyTextFormat)
		{
			return await SendAsync(recipients, subject, body, bodyTextFormat);
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
				message.To.Add(MailboxAddress.Parse(recipient));
			message.Subject = subject;
			message.Body = new TextPart((TextFormat)bodyTextFormat) { Text = body };
			var result = new OperationResult<bool>(false);
			using (var client = new SmtpClient())
			{
				try
				{
					if (await ConnectAsync(client, cancellationToken))
					{
						if (await AuthenticateAsync(client, cancellationToken))
						{
							await client.SendAsync(message, cancellationToken);
							result = new OperationResult<bool>(true);
							OnLogMessage("Email sent successfully.");
						}
					}
				}
				catch (Exception ex)
				{
					OnLogMessage($"Error sending email: {ex.Message}");
					result = new OperationResult<bool>(ex);
				}
				finally
				{
					await DiconnectAsync(client, cancellationToken);
				}
			}
			return result;
		}

		#endregion

		#region Validation

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

		bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			bool allow = Account.TrustServerCertificate;
			// No errors were found.
			if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
			{
				// Allow this client to communicate with unauthenticated servers.
				return true;
			}
			string message = $"Trust Server Certificate: {Account.TrustServerCertificate}.\r\n";
			message += string.Format("Certificate error: {0}", sslPolicyErrors);
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
			return allow;
		}


		#endregion

		#region Shared Methods

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

		public async Task<bool> ConnectAsync(MailService client, CancellationToken cancellationToken = default)
		{
			OnLogMessage("Connecting...");
			var isImap = client is ImapClient;
			var host = isImap
				? Account.ImapHost
				: Account.SmtpHost;
			int port = isImap
				? Account.ImapPort
				: Account.SmtpPort;
			var security = isImap
				? (SecureSocketOptions)Account.ImapSecurity
				: (SecureSocketOptions)Account.SmtpSecurity;
			client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
			await client.ConnectAsync(host, port, security, cancellationToken);
			if (client.IsConnected)
				OnLogMessage("Connected.");
			else
				OnLogMessage("Failed to connect.");
			return client.IsConnected;
		}

		public async Task<bool> AuthenticateAsync(MailService client, CancellationToken cancellationToken = default)
		{
			var isImap = client is ImapClient;
			var isMicrosoft =
				Account.ImapHost.Contains("office365.com") ||
				Account.ImapHost.Contains("office.com");
			var scope = isImap
				? ScopeImap
				: ScopeSmtp;
			if (isMicrosoft)
			{
				var accessToken = await getAccessToken(ScopeEmail, ScopeOpenId, ScopeOfflineAccess, scope);
				await client.AuthenticateAsync(accessToken, cancellationToken);
			}
			else
			{
				await client.AuthenticateAsync(Account.Username, Account.Password, cancellationToken);
			}
			if (client.IsAuthenticated)
				OnLogMessage("Authenticated.");
			else
				OnLogMessage("Failed to authenticate.");
			return client.IsAuthenticated;
		}

		public async Task DiconnectAsync(MailService client, CancellationToken cancellationToken = default)
		{
			if (client.IsConnected)
			{
				await client.DisconnectAsync(true, cancellationToken);
				OnLogMessage("Disconnected from the server.");
			}
		}

		#endregion

		#region Microsoft Azure Active Directory

		// https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth-ropc

		const string ScopeEmail = "email";
		const string ScopeOpenId = "openid";
		const string ScopeOfflineAccess = "offline_access";
		const string ScopeImap = "https://outlook.office.com/IMAP.AccessAsUser.All";
		const string ScopeSmtp = "https://outlook.office.com/SMTP.Send";
		const string SmtpHost = "smtp.office365.com";
		const string ImapHost = "outlook.office365.com";
		const string TenantId = "<GUID>";
		const string AppId = "<GUID>";
		const string AppSecret = "<secret value>";

		async Task PrintInbox(CancellationToken cancellationToken = default)
		{
			var ms = new MemoryStream();
			var logger = new ProtocolLogger(ms, true);
			using (var client = new ImapClient(logger))
			{
				try
				{
					if (await ConnectAsync(client, cancellationToken))
					{
						if (await AuthenticateAsync(client, cancellationToken))
						{
							client.Inbox.Open(FolderAccess.ReadOnly);
							var emailUIDs = client.Inbox.Search(SearchQuery.New);
							OnLogMessage($"Found {emailUIDs.Count} new emails in the {Account.Username} inbox");
							foreach (var emailUID in emailUIDs)
							{
								var email = client.Inbox.GetMessage(emailUID);
								OnLogMessage($"Got email from {email.From[0]} on {email.Date}: {email.Subject}");
							}
						}
					}
				}
				catch (Exception e)
				{
					OnLogMessage($"Error in 'print inbox': {e.GetType().Name} {e.Message}");
				}
				finally
				{
					await DiconnectAsync(client, cancellationToken);
				}
			}
		}

		/// <summary>
		/// Get the access token using the ROPC grant (<see cref="https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth-ropc"/>).
		/// </summary>
		/// <param name="scopes">The scopes/permissions the app requires</param>
		/// <returns>An access token that can be used to authenticate using MailKit.</returns>
		private async Task<SaslMechanismOAuth2> getAccessToken(params string[] scopes)
		{
			if (scopes == null || scopes.Length == 0) throw new ArgumentException("At least one scope is required", nameof(scopes));

			var scopesStr = string.Join(" ", scopes.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)));
			var content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("grant_type", "password"),
				new KeyValuePair<string, string>("username", Account.Username),
				new KeyValuePair<string, string>("password", Account.Password),
				new KeyValuePair<string, string>("client_id", AppId),
				new KeyValuePair<string, string>("client_secret", AppSecret),
				new KeyValuePair<string, string>("scope", scopesStr),
			});
			using (var client = new HttpClient())
			{
				var response = await client.PostAsync($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token", content).ConfigureAwait(continueOnCapturedContext: false);
				var responseString = await response.Content.ReadAsStringAsync();
				var json = JObject.Parse(responseString);
				var token = json["access_token"];
				return token != null
					? new SaslMechanismOAuth2(Account.Username, token.ToString())
					: null;
			}
		}

		#endregion


	}

}
