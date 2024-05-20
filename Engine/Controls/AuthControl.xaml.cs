using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
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
			await LoadUserProfile();
		}

		private async void SignOutButton_Click(object sender, RoutedEventArgs e)
		{
			var success = await Global.Security.SignOut();
			LogPanel.Add(
				success
				? "User signed out successfully.\r\n"
				: "No user is currently signed in.\r\n"
				);

			AuthIconPanel.UserName.Text = string.Empty;
			AuthIconPanel.UserAvatar.Source = null;
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Allows to run mehod once when control is created.
			if (ControlsHelper.AllowLoad(this))
			{
				await Global.Security.LoadCurrentAccount();
				await LoadUserProfile();
			}
		}

		/// <summary>
		/// Test getting information from azure by using access token.
		/// </summary>
		public async Task FetchAzureInformation()
		{
			// Load saved user profile
			var profileResult = Global.Security.GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors) + "\r\n");
				return;
			}
			var profile = profileResult.Result;
			using (var httpClient = new HttpClient())
			{
				try
				{
					httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
					var response = await httpClient.GetAsync(TestTextBox.Text);
					if (!response.IsSuccessStatusCode)
					{
						LogPanel.Add($"{response}\r\n");
						return;
					}
					var contents = await response.Content.ReadAsStringAsync();
					LogPanel.Add($"{contents}\r\n");
				}
				catch (System.Exception ex)
				{
					LogPanel.Add($"{ex}\r\n");
				}
			}
		}

		/// <summary>
		/// Test getting secret from azure key vault by using ClientSecretCredential(tenantId, clientId, clientSecret).
		/// </summary>
		public async Task<string> GetSecretFromKeyVaultAsync()
		{
			LogPanel.Clear();
			var keyVaultName = KeyVaultNameTextBox.Text;
			var tenantId = TenantIdTextBox.Text;
			var clientId = ClientIdTextBox.Text;
			var clientSecret = ClientSecretPasswordBox.Password;
			var secretName = SecretNameTextBox.Text;
			try
			{
				// Azure Key Vault URI
				string kvUri = $"https://{keyVaultName}.vault.azure.net/";
				// Create a new secret client
				var client = new SecretClient(new Uri(kvUri), new ClientSecretCredential(tenantId, clientId, clientSecret));
				// Retrieve the secret from Azure Key Vault
				KeyVaultSecret secret = await client.GetSecretAsync(secretName);
				LogPanel.Add($"{secret.Value}\r\n");
				return secret.Value;
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString() + "\r\n");
			}
			return null;
		}

		/// <summary>
		/// Test getting secret from azure key vault by using AccessTokenCredential(userProfile.AccessToken).
		/// </summary>
		/// <returns></returns>
		public async Task<string> GetSecretFromKeyVaultAsyncUseProfile()
		{
			var profileResult = Global.Security.GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors) + "\r\n");
				return null;
			}
			var keyVaultName = KeyVaultNameTextBox.Text;
			var secretName = SecretNameTextBox.Text;
			var userProfile = profileResult.Result;
			try
			{
				// Azure Key Vault URI
				var kvUri = $"https://{keyVaultName}.vault.azure.net/";

				// Create a new secret client with the existing access token
				var client = new SecretClient(new Uri(kvUri), new AccessTokenCredential(userProfile.AccessToken));

				// Retrieve the secret from Azure Key Vault
				KeyVaultSecret secret = await client.GetSecretAsync(secretName);
				LogPanel.Add($"{secret.Value}\r\n");
				return secret.Value;
			}
			catch (Exception ex)
			{
				LogPanel.Add($"{ex}\r\n");
			}
			return null;
		}


		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			LogPanel.Clear();
			var profileResult = Global.Security.GetProfile();
			if (profileResult.Success)
				InspectToken(profileResult.Result.IdToken);
			await FetchAzureInformation();
		}


		public async Task LoadUserProfile()
		{
			// Load saved user profile
			var profileResult = Global.Security.GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors) + "\r\n");
				return;
			}
			var profile = profileResult.Result;
			AuthIconPanel.UserName.Text = profile.Username;
			AuthIconPanel.UserAvatar.Source = await Global.Security.GetUserAvatar(profile.AccessToken);
		}

		public void InspectToken(string token)
		{
			var handler = new JwtSecurityTokenHandler();
			try
			{
				var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
				if (jwtToken == null)
				{
					LogPanel.Add("Invalid JWT token.\r\n");
					return;
				}
				// Display the expiry date
				var expiryDate = jwtToken.ValidTo;
				LogPanel.Add("Token Expiry Date: {expiryDate}\r\n");
				// Optionally, you can print other claims as well
				foreach (var claim in jwtToken.Claims)
					Console.WriteLine($"Claim Type: {claim.Type}, Claim Value: {claim.Value}\r\n");
			}
			catch (Exception ex)
			{
				LogPanel.Add($"{ex}\r\n");
			}
		}


	}

}
