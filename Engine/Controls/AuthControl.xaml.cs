using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AuthControl.xaml
	/// </summary>
	public partial class AuthControl : UserControl
	{
		private IPublicClientApplication _pca;
		private IAccount _account;

		public AuthControl()
		{
			InitializeComponent();
		}

		private async void SignInButton_Click(object sender, RoutedEventArgs e)
		{
			LogPanel.Clear();
			var scopes = new string[] { "User.Read" };
			AuthenticationResult result;
			try
			{
				// The application requests User.Read permission,
				// which allows it to sign in the user and read their basic profile information.

				/*

				When trying to sign-in for the first time into `user.name@company.com` Azure account user will see
				"Approval Required" dialog if appliction has not been certified or previously approved within the "Company.com" organization's Azure AD.
				"Company.com" administrator must approve these permissions for the application:
					- Sign in and read the user profile:
					  This allows the app to sign in users and read their basic profile information.
					- Maintain access to data the user has given it access to:
				      This allows the app to retain access to the granted resources
					  without needing further explicit user consent repeatedly.

				Who Needs to Approve?
				IT or Domain Administrator of `company.com` domain.

				AI Companion asks for `User.Read` permission that typically grants the application the ability to:

					- Access the user’s basic profile info.
					- Access the user’s email address.
					- Access the user’s display name.

					Applciaiton This data will be stored on the local PC.

				*/

				_account = (await _pca.GetAccountsAsync()).FirstOrDefault();
				// Get result that includes access tokens that grant the application the rights to fetch user information.
				result = await _pca.AcquireTokenSilent(scopes, _account).ExecuteAsync();
			}
			catch (MsalUiRequiredException)
			{
				result = await _pca.AcquireTokenInteractive(scopes).ExecuteAsync();
			}
			catch
			{
				return;
			}
			_account = result.Account;
			UserName.Text = result.Account.Username;
			var avatarUrl = await LoadUserAvatar(result.AccessToken);
			SaveUserProfile(result, avatarUrl);
		}


		/// <summary>
		/// Get user profile with the access token.
		/// </summary>
		public OperationResult<UserProfile> GetProfile()
		{
			var profile = Global.AppSettings.UserProfiles.FirstOrDefault(p => p.ServiceType == ApiServiceType.Azure);
			if (profile == null)
				return new OperationResult<UserProfile>(new Exception("Profile not found. Please log-in."));
			if (string.IsNullOrEmpty(profile.AccessToken))
				return new OperationResult<UserProfile>(new Exception("No valid user profile or access token found."));
			return new OperationResult<UserProfile>(profile);
		}


		private void SaveUserProfile(AuthenticationResult result, string avatarUrl)
		{
			var account = result.Account;
			// Save User profile.
			var profile = Global.AppSettings.UserProfiles
				.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure && x.Username == account.Username);
			if (profile == null)
			{
				profile = new UserProfile()
				{
					ServiceType = ApiServiceType.Azure,
					Username = account.Username,
				};
				Global.AppSettings.UserProfiles.Add(profile);
			}
			profile.AccessToken = result.AccessToken;
			profile.AvatarUrl = avatarUrl;
		}

		private async Task LoadUserProfile()
		{
			// Load saved user profile
			var profileResult = GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors));
				return;
			}
			var profile = profileResult.Result;
			UserName.Text = profile.Username;
			await LoadUserAvatar(profile.AvatarUrl);
		}

		private async Task<string> LoadUserAvatar(string accessToken)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value");
				if (response.IsSuccessStatusCode)
				{
					var avatarUrl = response.RequestMessage.RequestUri.ToString();
					var stream = await response.Content.ReadAsStreamAsync();
					var bitmap = new BitmapImage();
					bitmap.BeginInit();
					bitmap.StreamSource = stream;
					bitmap.CacheOption = BitmapCacheOption.OnLoad;
					bitmap.EndInit();
					UserAvatar.Source = bitmap;
					return avatarUrl;
				}
			}
			return null;
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				_pca = PublicClientApplicationBuilder.Create(Global.AppSettings?.ClientAppId)
					.WithRedirectUri("http://localhost")
					.WithDefaultRedirectUri()
					.Build();

				await LoadUserProfile();
			}
		}

		/// <summary>
		/// Test getting information from azure by using access token.
		/// </summary>
		public async Task FetchAzureInformation()
		{
			LogPanel.Clear();
			// Load saved user profile
			var profileResult = GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors));
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
						LogPanel.Add("API request failed with status code: " + response.StatusCode);
						return;
					}
					var contents = await response.Content.ReadAsStringAsync();
					LogPanel.Add(contents);
				}
				catch (System.Exception ex)
				{
					LogPanel.Add(ex.ToString());
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
				LogPanel.Add(secret.Value);
				return secret.Value;
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString());
			}
			return null;
		}

		/// <summary>
		/// Test getting secret from azure key vault by using AccessTokenCredential(userProfile.AccessToken).
		/// </summary>
		/// <returns></returns>
		public async Task<string> GetSecretFromKeyVaultAsyncUseProfile()
		{
			var profileResult = GetProfile();
			if (!profileResult.Success)
			{
				LogPanel.Add(string.Join("\r\n", profileResult.Errors));
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
				LogPanel.Add(secret.Value);
				return secret.Value;
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString());
			}
			return null;
		}


		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			await FetchAzureInformation();
		}
	}

}
