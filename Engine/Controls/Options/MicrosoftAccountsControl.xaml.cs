﻿using Azure.Core;
using Azure.Identity;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using Microsoft.Identity.Client;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for MicrosoftAccountsControl.xaml
	/// </summary>
	public partial class MicrosoftAccountsControl : UserControl, INotifyPropertyChanged
	{
		public MicrosoftAccountsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			var debugVisibility = InitHelper.IsDebug ? Visibility.Visible : Visibility.Collapsed;
			KeyVaultSettings.Visibility = debugVisibility;
			TestGroupBox.Visibility = debugVisibility;
		}

		private async void SignInButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				LogPanel.Clear();
				var scopes = new string[] { MicrosoftAccountManager.MicrosoftGraphScope };
				await MicrosoftAccountManager.Current.SignIn(scopes, cancellationToken);
				await MicrosoftAccountManager.Current.RefreshProfileImage(cancellationToken);
			});
		}

		private async void SignOutButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var success = await MicrosoftAccountManager.Current.SignOut();
				LogPanel.Add(
					success
					? "User signed out successfully.\r\n"
					: "No user is currently signed in.\r\n"
					);
				await MicrosoftAccountManager.Current.RefreshProfileImage(cancellationToken);
			});
		}

		public void InspectToken(string accessToken)
		{
			// header, payload/body, signature.
			var isJwtToken = accessToken.Split('.').Length == 3;
			// If token format is JSON Web Token (JWT) then...
			if (isJwtToken)
			{
				// JWT tokens allow clients to decode and validate them.
				var handler = new JwtSecurityTokenHandler();
				var jwtToken = handler.ReadToken(accessToken) as JwtSecurityToken;
				if (jwtToken == null)
				{
					LogPanel.Add("Invalid JWT token.\r\n");
					return;
				}
				// Display the expiry date
				LogPanel.Add($"Claims[{jwtToken.Claims.Count()}]: \r\n");
				// Print claims.
				foreach (var claim in jwtToken.Claims)
					LogPanel.Add($"  {claim.Type}: {claim.Value}\r\n");
			}
			else
			{
				// An opaque token is a sequence of characters
				// not readable or interpretable by the client.
				LogPanel.Add("Opaque Access Token.\r\n");
				LogPanel.Add($"{accessToken}.\r\n");
			}
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var uri = new Uri(TestTextBox.Text);
				var scope1 = $"{uri.Scheme}://{uri.Host}/.default";
				var scopes = new[] { scope1 };
				var token = await MicrosoftAccountManager.Current.GetAccessToken(scopes, interactive: true, cancellationToken);
				var accessToken = token.Token;
				// Inspect the token
				InspectToken(accessToken);
				var contents = await MicrosoftAccountManager.Current.MakeAuthenticatedApiCall(TestTextBox.Text, accessToken, cancellationToken);
				LogPanel.Add($"{contents}\r\n");
			}, true);
		}

		//var token1 = GetAccessTokenUsingWindowsAuthentication();
		//var content = await GetWebPageContentsWithDefaultAzureCredential(TestTextBox.Text, scope1, token1);
		//var w = new JocysCom.VS.AiCompanion.Plugins.Core.Web();
		//var page = await w.GetWebPageContentsAuthenticated(TestTextBox.Text, false);

		public async Task<string> GetAccessTokenAsync()
		{
			try
			{
				var result = await MicrosoftAccountManager.Current.Pca.AcquireTokenInteractive(new[] { MicrosoftAccountManager.MicrosoftAzureManagementScope }).ExecuteAsync();
				return result.AccessToken;
			}
			catch (MsalException ex)
			{
				Console.WriteLine($"Error acquiring access token: {ex.Message}");
				return null;
			}
		}

		#region Test

		private async void CachedAccountsButton_Click(object sender, RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var accounts = await MicrosoftAccountManager.Current.Pca.GetAccountsAsync();
				LogPanel.Add($"Cached Accounts [{accounts.Count()}]:\r\n");
				foreach (var account in accounts)
				{
					LogPanel.Add($"Username: {account.Username}\r\n");
					LogPanel.Add($"HomeAccountId: {account.HomeAccountId}\r\n");
					LogPanel.Add($"Environment: {account.Environment}\r\n");
					LogPanel.Add("\r\n");
				}
			}, true);
		}

		private async void TokensButton_Click(object sender, RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				await Task.Delay(0);
				var scope = new[] { MicrosoftAccountManager.MicrosoftGraphScope };
				var token = await MicrosoftAccountManager.Current.GetAccessToken(scope, interactive: true, cancellationToken);
				var accessToken = token.Token;
				LogPanel.Add($"Access Token:\r\n");
				LogPanel.Add($"  Expiry Date: {token.ExpiresOn}\r\n");
				InspectToken(accessToken);
				var idToken = MicrosoftAccountManager.Current.GetProfile().IdToken;
				if (!string.IsNullOrEmpty(idToken))
				{
					LogPanel.Add($"ID Token:\r\n");
					InspectToken(idToken);
				}
			}, true);
		}

		private async void UserInfoButton_Click(object sender, RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var user = await MicrosoftAccountManager.Current.GetMicrosoftUser(cancellationToken);
				LogAsJson(user);
			}, true);
		}

		private async void SubscriptionsButton_Click(object sender, RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				LogPanel.Add("Retrieved Subscriptions:");
				var items = await MicrosoftAccountManager.Current.GetAzureSubscriptions(cancellationToken);
				foreach (var item in items)
					LogPanel.Add($"Subscription: {item.Key} - {item.Value}\r\n");
			}, true);
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			var items = cancellationTokenSources.ToArray();
			foreach (var item in items)
				item.Cancel();
		}


		#endregion

		#region Control Helper Methods

		public void LogAsJson(object o)
		{
			var options = new JsonSerializerOptions()
			{
				WriteIndented = true,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
			};
			var json = JsonSerializer.Serialize(o, options);
			LogPanel.Add($"{json}\r\n");

		}

		/// <summary>
		/// Stores cancellation tokens created on this control that can be stopped with the [Stop] button.
		/// </summary>
		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		/// <summary>
		/// Helps run cancellable methods of this form and logs results to the log panel.
		/// </summary>
		async Task ExecuteMethod(Func<CancellationToken, Task> action, bool requiresSignIn = false)
		{

			LogPanel.Clear();
			var profile = MicrosoftAccountManager.Current.GetProfile();
			if (requiresSignIn && !profile.IsSigned)
			{
				LogPanel.Add($"Please sign in first.\r\n");
				return;
			}
			var source = new CancellationTokenSource();
			source.CancelAfter(TimeSpan.FromSeconds(600));
			cancellationTokenSources.Add(source);
			Global.MainControl.InfoPanel.AddTask(source);
			try
			{
				await action.Invoke(source.Token);
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString());
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			finally
			{
				cancellationTokenSources.Remove(source);
				Global.MainControl.InfoPanel.RemoveTask(source);
			}
		}

		/// <summary>
		/// Get the content of a web page using DefaultAzureCredential.
		/// </summary>
		private async Task<string> GetWebPageContentsWithDefaultAzureCredential(string url, string scope, string accessToken, CancellationToken cancellationToken = default)
		{
			// Define the scope required to access the target resource
			var scopes = new[] { scope };
			//var accessToken = await AppSecurityHelper.GetAccessToken(scopes, cancellationToken);
			// Make the authenticated HTTP request
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var response = await client.GetAsync(url, cancellationToken);
				if (response.IsSuccessStatusCode)
				{
					// Successfully fetched the content
					string content = await response.Content.ReadAsStringAsync();
					return content;
				}
				else
				{
					// Handle unsuccessful responses
					return $"Error: Unable to fetch the page. Status Code: {response.StatusCode}";
				}
			}
		}


		/// <summary>
		/// Get access token using Windows Authentication
		/// </summary>
		/// <returns>Access Token</returns>
		public static async Task<string> GetAccessTokenUsingWindowsAuthentication()
		{
			// Use DefaultAzureCredential which includes multiple authentication methods
			var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
			{
				ExcludeInteractiveBrowserCredential = true,
				ExcludeManagedIdentityCredential = true,
				ExcludeVisualStudioCredential = true,
				ExcludeAzureCliCredential = true,
				ExcludeEnvironmentCredential = true,
				ExcludeSharedTokenCacheCredential = false // Ensure this one is included
			});

			var tokenRequestContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }); // Adjust scope based on resource
			AccessToken token = await credential.GetTokenAsync(tokenRequestContext);
			return token.Token;
		}


		#endregion

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				var profile = MicrosoftAccountManager.Current.GetProfile();
				profile.PropertyChanged += Profile_PropertyChanged;
			}
		}

		private void Profile_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserProfile.IsSigned))
				OnPropertyChanged(nameof(UserIsSigned));

		}

		public bool UserIsSigned => MicrosoftAccountManager.Current.GetProfile().IsSigned;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion


	}
}