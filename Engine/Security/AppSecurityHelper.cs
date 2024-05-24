using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public class AppSecurityHelper
	{

		/*

		SECURITY:

			Credential - An object used to prove the identity of a user or service.
						 It can contain a username and password, tokens, certificates, or other authentication information.
			Scope      - A string that specifies a particular resource and set of permissions requested for that resource.
						 Scopes are supplied with credentials when requesting an access token.
			Token      - A digital object created after a user or service successfully authenticates.
						 Tokens require credentials and scopes to be created.
			Account    - An object representing the authenticated user's identity.

		TOKEN TYPES:

			- Access Token - Sent instead of credentials to authorize access to resources on behalf of the user.
			- ID Token - Contains identity information about the user. They should NOT be sent to an API.

		An Access Token must contain:

			- Expiry Date: The date and time after which the token is no longer valid.
			- Scope: The list of resources or operations that the token grants access to.
			- Permissions: The specific actions that the token holder is allowed to perform on the resources.
			- Digital Signature: A cryptographic signature that can be used to verify the token's integrity and authenticity, proving that it was issued by the genuine provider.

		USER/SERVICE 
		│
		│    AUTHENTICATION SERVER: Verifies Credentials
		├──► Credentials (e.g., username & password, token, certificate) + Optional Scopes
		│◄── Tokens (Access Token, ID Token)
		│
		│    RESOURCE SERVER/SERVICE: Verifies Token (Using Digital Signature)
		├──► Access Token
		│◄── Resource

		*/

		// Preconfigured set of permissions. Usually formatted as https://{resource}/{permission}:
		public const string MicrosoftGraphScope = "https://graph.microsoft.com/.default";
		public const string MicrosoftAzureScope = "https://management.azure.com/.default";
		// Fully qualified URI for "User.Read";
		// Permissions to sign in the user and read the user's profile.
		public const string MicrosoftGraphUserReadScope = "https://graph.microsoft.com/User.Read";

		private const string CommonAuthority = "https://login.microsoftonline.com/common";
		private const string OrganizationsAuthority = "https://login.microsoftonline.com/organizations";
		private const string ConsumersAuthority = "https://login.microsoftonline.com/consumers";
		private const string TenantAuthority = "https://login.microsoftonline.com/{0}";

		// Using DefaultAzureCredential
		//
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
			//.WithAuthority(CommonAuthority)
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
		public static OperationResult<UserProfile> GetProfile()
		{
			var profile = Global.AppSettings.UserProfiles.FirstOrDefault(p => p.ServiceType == ApiServiceType.Azure);
			if (profile == null)
				return new OperationResult<UserProfile>(new Exception("Profile not found. Please log-in."));
			//if (string.IsNullOrEmpty(profile.AccessToken))
			//	return new OperationResult<UserProfile>(new Exception("No valid user profile or access token found."));
			return new OperationResult<UserProfile>(profile);
		}

		public async Task RefreshProfileImage()
		{
			var profile = GetProfile().Result;
			var accessToken = profile?.AccessToken;
			if (string.IsNullOrEmpty(accessToken))
			{
				var token = await GetAccessToken(new string[] { MicrosoftGraphScope });
				accessToken = token.Token;
			}
			profile.Image = await Global.Security.GetUserAvatar(accessToken);
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

		/// <summary>
		/// // By default return credentials that user used to sign in. 
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<TokenCredential> GetTokenCredential(CancellationToken cancellationToken = default)
		{
			var credentials = await GetAppTokenCredential(cancellationToken);
			if (credentials != null)
				return credentials;
			return await GetWinTokenCredentials();
		}

		/// <summary>
		/// Get the credentials signed into the current app. Get refreshed token if it is expired.
		/// </summary>

		public static async Task<TokenCredential> GetAppTokenCredential(CancellationToken cancellationToken = default)
		{
			var accessToken = GetProfile().Result?.AccessToken;
			if (string.IsNullOrEmpty(accessToken))
				return null;
			// Ensure the token has the required scopes
			var scopes = new string[] { MicrosoftGraphScope };
			var tokenCredential = new AccessTokenCredential(accessToken);
			var tokenRequestContext = new TokenRequestContext(scopes);
			var token = tokenCredential.GetToken(tokenRequestContext, cancellationToken);
			var isExired = token.ExpiresOn < DateTimeOffset.UtcNow;
			if (isExired)
			{
				// Re-acquire the token with the required scopes
				var result = await Global.Security.SignIn(scopes);
				if (!result.Success)
					return null;
			}
			var credential = new AccessTokenCredential(accessToken);
			return credential;
		}

		/// <summary>
		/// Get windows credentials the app is running with.
		/// </summary>
		public static async Task<TokenCredential> GetWinTokenCredentials()
		{
			await Task.Delay(0);
			var options = new DefaultAzureCredentialOptions();
			options.ExcludeInteractiveBrowserCredential = false;
			// Default credentials of the Azure environment in which application is running.
			// Credentials currently used to log into Windows.
			var credential = new DefaultAzureCredential(options);
			return credential;
		}

		#endregion

		#region SignIn and SignOut

		public async Task<OperationResult<AuthenticationResult>> SignIn(params string[] scopes)
		{
			if (scopes.Length == 0)
				scopes = new string[] { MicrosoftGraphScope };
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

		/// <summary>
		/// Get access token.
		/// </summary>
		/// <param name="scopes">Set of permissions.</param>
		public static async Task<AccessToken> GetAccessToken(string[] scopes, CancellationToken cancellationToken = default)
		{
			// Define credentials that will be used to access resources.
			var credential = await GetTokenCredential(cancellationToken);
			// Add wanted permissions
			var context = new TokenRequestContext(scopes);
			// Get access token.
			var token = await credential.GetTokenAsync(context, cancellationToken);
			return token;
		}

		public IAccount _account;


		static async Task<Microsoft.Graph.Models.User> GetUserInfoAsync(string accessToken)
		{
			using (var httpClient = new HttpClient())
			{
				// Set the Authorization header with the access token
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				// Set the base address for the Microsoft Graph API
				httpClient.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
				// Send a GET request to the /me endpoint
				var response = await httpClient.GetAsync("me");
				// Ensure the request was successful
				response.EnsureSuccessStatusCode();
				// Read and deserialize the JSON response
				var jsonResponse = await response.Content.ReadAsStringAsync();
				var userInfo = System.Text.Json.JsonSerializer.Deserialize<Microsoft.Graph.Models.User>(jsonResponse);
				return userInfo;
			}
		}

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

		public async Task<BitmapImage> GetUserAvatar(string accessToken, CancellationToken cancellationToken = default)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value", cancellationToken);
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


		public static async Task<Dictionary<string, string>> GetSubscriptionNamesAndIdsAsync(TokenCredential credential, CancellationToken cancellationToken = default)
		{
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
			catch
			{
				throw;
			}
			finally
			{
				await enumerator.DisposeAsync();
			}
			return subscriptionsDict;
		}

		#region API Calls

		public static async Task<string> MakeAuthenticatedApiCall(
			string url, string accessToken,
			CancellationToken cancellationToken = default)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync(url, cancellationToken);
				var contents = await response.Content.ReadAsStringAsync();
				return contents;
			}
		}

		public static async Task<string> MakeAuthenticatedCall(string url, CancellationToken cancellationToken = default)
		{
			// Define the scope required to access the target resource
			// This is a common example, you should replace it with the correct scope for your resource
			var scopes = new string[] { $"{url}/.default" };
			var token = await GetAccessToken(scopes, cancellationToken);
			// Set up HttpClient with the acquired token
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
				// Make request to the target URL
				HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
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

		#endregion

		#region Key Vault

		public static async Task<string> GetSecretFromKeyVault(
			string keyVaultName, string secretName, string accessToken,
			CancellationToken cancellationToken = default)
		{
			var credential = new AccessTokenCredential(accessToken);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential, cancellationToken);
		}

		public static async Task<string> GetSecretFromKeyVault(
			string keyVaultName, string secretName, string tenantId, string clientId, string clientSecret,
			CancellationToken cancellationToken = default)
		{
			var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential, cancellationToken);
		}

		public static async Task<string> GetSecretFromKeyVault(
			string keyVaultName, string secretName, TokenCredential credential,
			CancellationToken cancellationToken = default)
		{
			// Azure Key Vault URI
			string kvUri = $"https://{keyVaultName}.vault.azure.net/";
			// Create a new secret client
			var client = new SecretClient(new Uri(kvUri), credential);
			// Retrieve the secret from Azure Key Vault
			KeyVaultSecret secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
			return secret.Value;
		}

		#endregion

	}
}
