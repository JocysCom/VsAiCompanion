using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary;
using Microsoft.Identity.Client;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public class AppSecurityHelper
	{
		// Credential - An object used to prove the identity of a user or service.
		// Token - Digital objects created after a user or service successfully authenticates and passes authorization processes. Requires credentials to be created.
		// Account - An object representing the authenticated user's identity.

		// Using DefaultAzureCredential
		// The DefaultAzureCredential includes a chain of nine different credential types,
		// and it will attempt to authenticate using each of these in turn, stopping when one succeeds.
		// These credentials include:
		// 1.EnvironmentCredential
		// 2.ManagedIdentityCredential
		// 3.SharedTokenCacheCredential
		// 4.VisualStudioCredential
		// 5.VisualStudioCodeCredential
		// 6.AzureCliCredential
		// 7.InteractiveBrowserCredential
		// 8.AzurePowerShellCredential
		// 9.InteractiveBrowserCredential

		public IPublicClientApplication Pca { get; } = PublicClientApplicationBuilder
				.Create(Global.AppSettings?.ClientAppId)
				.WithRedirectUri("http://localhost")
				.WithDefaultRedirectUri()
				.Build();


		/// <summary>
		/// Store token cache in the app settings.
		/// </summary>
		/// <param name="tokenCache"></param>
		public void EnableTokenCache(ITokenCache tokenCache)
		{
			tokenCache.SetBeforeAccess(BeforeAccessNotification);
			tokenCache.SetAfterAccess(AfterAccessNotification);
		}

		private void BeforeAccessNotification(TokenCacheNotificationArgs args)
		{
			args.TokenCache.DeserializeMsalV3(Global.AppSettings.AzureTokenCache);
		}

		private void AfterAccessNotification(TokenCacheNotificationArgs args)
		{
			// if the access operation resulted in a cache update
			if (args.HasStateChanged)
				Global.AppSettings.AzureTokenCache = args.TokenCache.SerializeMsalV3();
		}

		#region User Profile

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

		public async Task RefreshProfileImage()
		{
			var profile = GetProfile()?.Result;
			if (profile == null)
				return;
			profile.Image = await Global.Security.GetUserAvatar(profile.AccessToken);
		}

		private void SaveUserProfile(AuthenticationResult result)
		{
			var account = result.Account;

			// Save User profile.
			var profile = Global.AppSettings.UserProfiles
				.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure);
			if (profile == null)
			{
				profile = new UserProfile();
				Global.AppSettings.UserProfiles.Add(profile);
			}
			profile.ServiceType = ApiServiceType.Azure;
			profile.Username = account.Username;
			profile.AccountId = account.HomeAccountId.Identifier;
			profile.AccessToken = result.AccessToken;
			profile.IdToken = result.IdToken;

			// Parse the ID token to extract user claims
			var handler = new JwtSecurityTokenHandler();
			var jsonToken = handler.ReadToken(result.IdToken) as JwtSecurityToken;
			var claims = jsonToken?.Claims;
			profile.Name = claims?.FirstOrDefault(c => c.Type == "name")?.Value;
		}


		#endregion

		#region SignIn and SignOut

		public async Task<OperationResult<AuthenticationResult>> SignIn(params string[] scopes)
		{
			if (scopes.Length == 0)
				scopes = new string[] { "User.Read" };
			AuthenticationResult result = null;
			var requiresUI = false;
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

				var accounts = await Pca.GetAccountsAsync();
				var accountId = GetProfile()?.Result?.AccountId;
				if (accountId == null)
				{
					requiresUI = true;
				}
				else
				{
					_account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == accountId);
					if (_account == null)
					{
						requiresUI = true;
					}
					else
					{
						// Get result that includes access tokens that grant the application the rights to fetch user information.
						result = await Pca.AcquireTokenSilent(scopes, _account).ExecuteAsync();
					}
				}
			}
			catch (MsalUiRequiredException)
			{
				requiresUI = true;
			}
			catch (Exception ex)
			{
				return new OperationResult<AuthenticationResult>(ex);
			}
			if (requiresUI)
			{
				try
				{
					result = await Pca.AcquireTokenInteractive(scopes).ExecuteAsync();
				}
				catch (Exception ex)
				{
					return new OperationResult<AuthenticationResult>(ex);
				}
			}
			_account = result?.Account;
			SaveUserProfile(result);
			return new OperationResult<AuthenticationResult>(result);
		}

		public async Task<bool> SignOut()
		{
			if (_account == null)
				return false;
			await Pca.RemoveAsync(_account);
			_account = null;
			var profile = GetProfile();
			profile.Result?.Clear();
			return true;
		}

		#endregion

		#region Authentication

		public static async Task<string> MakeAuthenticatedApiCall(string url, string accessToken)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync(url);
				var contents = await response.Content.ReadAsStringAsync();
				return contents;
			}
		}

		public static async Task<string> MakeAuthenticatedCall(string url)
		{
			// Define the scope required to access the target resource
			// This is a common example, you should replace it with the correct scope for your resource
			string[] scopes = new string[] { $"{url}/.default" };
			// Create DefaultAzureCredential instance
			var credential = new DefaultAzureCredential();
			// Get the token using the DefaultAzureCredential
			AccessToken token = await credential.GetTokenAsync(new TokenRequestContext(scopes));
			// Set up HttpClient with the acquired token
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
				// Make request to the target URL
				HttpResponseMessage response = await httpClient.GetAsync(url);
				if (response.IsSuccessStatusCode)
				{
					string pageContent = await response.Content.ReadAsStringAsync();
					return pageContent;
				}
				else
				{
					Console.WriteLine($"Failed to retrieve the page. Status Code: {response.StatusCode}");
				}
			}
			return null;
		}


		public IAccount _account;

		/// <summary>
		/// Load this once when app starts.
		/// </summary>
		/// <returns></returns>
		public async Task LoadCurrentAccount()
		{
			EnableTokenCache(Pca.UserTokenCache);
			// Load saved user profile
			var profile = GetProfile()?.Result;
			if (profile == null)
				return;
			// Returns all the available accounts in the user token cache for the application.
			var accounts = await Pca.GetAccountsAsync();
			if (!accounts.Any())
				return;
			// Attempt to find the account with the saved UserId
			_account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == profile.AccountId);
		}

		public async Task<BitmapImage> GetUserAvatar(string accessToken)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value");
				if (response.IsSuccessStatusCode)
				{
					var stream = await response.Content.ReadAsStreamAsync();
					var bitmap = new BitmapImage();
					bitmap.BeginInit();
					bitmap.StreamSource = stream;
					bitmap.CacheOption = BitmapCacheOption.OnLoad;
					bitmap.EndInit();
					return bitmap;
				}
			}
			return null;
		}

		#endregion

		#region Key Vault

		public static async Task<string> GetSecretFromKeyVault(string keyVaultName, string secretName, string accessToken)
		{
			var credential = new AccessTokenCredential(accessToken);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential);
		}

		public static async Task<string> GetSecretFromKeyVault(string keyVaultName, string secretName, string tenantId, string clientId, string clientSecret)
		{
			var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential);
		}

		public static async Task<string> GetSecretFromKeyVault(string keyVaultName, string secretName, TokenCredential credential)
		{
			try
			{

				// Azure Key Vault URI
				string kvUri = $"https://{keyVaultName}.vault.azure.net/";
				// Create a new secret client
				var client = new SecretClient(new Uri(kvUri), credential);
				// Retrieve the secret from Azure Key Vault
				KeyVaultSecret secret = await client.GetSecretAsync(secretName);
				return secret.Value;
			}
			catch (Exception ex)
			{
				// Log error if needed.
				throw new Exception("Error getting secret from KeyVault", ex);
			}
		}

		#endregion

	}
}
