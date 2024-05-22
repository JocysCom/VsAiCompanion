using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AuthControl.xaml
	/// </summary>
	public partial class AuthControl : UserControl
	{
		public AuthControl()
		{
			InitializeComponent();
		}

		private async void SignInButton_Click(object sender, RoutedEventArgs e)
		{
			LogPanel.Clear();
			await Global.Security.SignIn();
			await Global.Security.RefreshProfileImage();
		}

		private async void SignOutButton_Click(object sender, RoutedEventArgs e)
		{
			var success = await Global.Security.SignOut();
			LogPanel.Add(
				success
				? "User signed out successfully.\r\n"
				: "No user is currently signed in.\r\n"
				);
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Allows to run mehod once when control is created.
			if (ControlsHelper.AllowLoad(this))
			{
				await Global.Security.LoadCurrentAccount();
				await Global.Security.RefreshProfileImage();
			}
		}

		/// <summary>
		/// Test getting information from azure by using access token.
		/// </summary>
		// In AuthControl.xaml.cs
		public async Task FetchAzureInformation()
		{
			try
			{
				var accessToken = GetProfile()?.AccessToken;
				if (string.IsNullOrEmpty(accessToken))
				{
					var credential = new DefaultAzureCredential();
					var token = await credential.GetTokenAsync(
						new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" })
					);
					accessToken = token.Token;
				}
				//InspectToken(profile.IdToken);
				var contents = await AppSecurityHelper.MakeAuthenticatedApiCall(TestTextBox.Text, accessToken);
				LogPanel.Add($"{contents}\r\n");
			}
			catch (Exception ex)
			{
				LogPanel.Add($"{ex}\r\n");
			}
		}


		#region Key Vault

		/// <summary>
		/// Test getting secret from azure key vault.
		/// </summary>
		public async Task<string> GetSecretFromKeyVaultAsync(bool useAccessToken)
		{
			LogPanel.Clear();
			try
			{
				string secret;
				if (useAccessToken)
				{
					var profile = GetProfile();
					if (profile == null)
						return null;
					secret = await AppSecurityHelper
					.GetSecretFromKeyVault(KeyVaultNameTextBox.Text, SecretNameTextBox.Text, profile.AccessToken);
				}
				else
				{
					secret = await AppSecurityHelper.GetSecretFromKeyVault(
						KeyVaultNameTextBox.Text, SecretNameTextBox.Text,
						TenantIdTextBox.Text, ClientIdTextBox.Text, ClientSecretPasswordBox.Password);
				}
				LogPanel.Add($"{secret}\r\n");
				return secret;
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString() + "\r\n");
			}
			return null;
		}

		#endregion

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			await FetchAzureInformation();
		}

		public void InspectToken(string idToken)
		{
			var handler = new JwtSecurityTokenHandler();
			try
			{
				var jwtToken = handler.ReadToken(idToken) as JwtSecurityToken;
				if (jwtToken == null)
				{
					LogPanel.Add("Invalid JWT token.\r\n");
					return;
				}
				// Display the expiry date
				var expiryDate = jwtToken.ValidTo;
				LogPanel.Add($"Token Expiry Date: {expiryDate}\r\n");
				// Optionally, you can print other claims as well
				foreach (var claim in jwtToken.Claims)
					Console.WriteLine($"Claim Type: {claim.Type}, Claim Value: {claim.Value}\r\n");
			}
			catch (Exception ex)
			{
				LogPanel.Add($"{ex}\r\n");
			}
		}

		public async Task<Dictionary<string, string>> GetSubscriptionNamesAndIdsAsync(CancellationToken cancellationToken = default)
		{
			// Initialize the Azure credentials using DefaultAzureCredential
			var credential = await GetCredentials();
			// Initialize the ArmClient
			var armClient = new ArmClient(credential);
			// Dictionary to store subscription names and IDs
			var subscriptionsDict = new Dictionary<string, string>();
			// Fetch the list of subscriptions and use the synchronous foreach loop with manual async handling
			var enumerator = armClient.GetSubscriptions().GetAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
			try
			{
				while (await enumerator.MoveNextAsync())
				{
					var subscription = enumerator.Current;
					subscriptionsDict.Add(subscription.Data.SubscriptionId, subscription.Data.DisplayName);
				}
			}
			finally
			{
				await enumerator.DisposeAsync();
			}
			return subscriptionsDict;
		}

		private async void ListSubscriptionsButton_Click(object sender, RoutedEventArgs e)
		{
			LogPanel.Clear();
			var ts = AddToken();
			try
			{
				var items = await GetSubscriptionNamesAndIdsAsync(ts.Token);
				foreach (var item in items)
					LogPanel.Add($"Subscription: {item.Key} - {item.Value}\r\n");
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString());
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			finally
			{
				Global.MainControl.InfoPanel.RemoveTask(ts);
			}
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			var items = cancellationTokenSources.ToArray();
			foreach (var item in items)
				item.Cancel();
		}

		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		public UserProfile GetProfile()
		{
			var profileResult = Global.Security.GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors) + "\r\n");
				return null;
			}
			return profileResult.Result;
		}

		public async Task<AccessTokenCredential> GetAppCredential(CancellationToken cancellationToken = default)
		{
			var profile = GetProfile();
			if (profile == null)
				return null;
			// Ensure the token has the required scopes
			var requiredScopes = new string[] { "https://management.azure.com/.default" };
			var tokenCredential = new AccessTokenCredential(profile.AccessToken);
			var tokenRequestContext = new TokenRequestContext(requiredScopes);
			var token = tokenCredential.GetToken(tokenRequestContext, cancellationToken);
			if (token.ExpiresOn < DateTimeOffset.UtcNow)
			{
				// Re-acquire the token with the required scopes
				var result = await Global.Security.SignIn(requiredScopes);
				if (!result.Success)
				{
					LogPanel.Add(string.Join("\r\n", result.Errors) + "\r\n");
					return null;
				}
			}
			var credential = new AccessTokenCredential(profile.AccessToken);
			return credential;
		}

		public async Task<TokenCredential> GetCredentials()
		{
			var credentials = await GetAppCredential();
			return (TokenCredential)credentials ??
				// Default credentials of the Azure environment in which application is running.
				// Credentials currently used to log into Windows.
				new DefaultAzureCredential();
		}

		public CancellationTokenSource AddToken()
		{
			var source = new CancellationTokenSource();
			source.CancelAfter(TimeSpan.FromSeconds(30));
			cancellationTokenSources.Add(source);
			Global.MainControl.InfoPanel.AddTask(source);
			return source;
		}

		private async void GetSecretWithAccessTokenButton_Click(object sender, RoutedEventArgs e)
		{
			await GetSecretFromKeyVaultAsync(true);
		}

		private async void GetSecretWithClientSecretButton_Click(object sender, RoutedEventArgs e)
		{
			await GetSecretFromKeyVaultAsync(false);
		}

		private async Task ListAccounts()
		{
			LogPanel.Clear();
			var accounts = await Global.Security.Pca.GetAccountsAsync();
			foreach (var account in accounts)
			{
				LogPanel.Add($"Username: {account.Username}\r\n");
				LogPanel.Add($"HomeAccountId: {account.HomeAccountId}\r\n");
				LogPanel.Add($"Environment: {account.Environment}\r\n");
				LogPanel.Add("\r\n");
			}
		}

		private async void ListAccountsButton_Click(object sender, RoutedEventArgs e)
			=> await ListAccounts();


	}
}
