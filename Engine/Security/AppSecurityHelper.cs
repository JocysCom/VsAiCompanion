using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
			//.WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.None)
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

		/// <summary>
		/// Determine if it is a Microsoft account by checking if the access token has a specific tenant ID for Microsoft accounts.
		/// </summary>
		public static async Task<bool> IsMicrosoftAccount()
		{
			var profile = GetProfile();
			// If access token is empty then application must use
			// user 
			var accessToken = profile.IdToken ?? profile.AccessToken;
			TokenCredential credential = await GetTokenCredential();
			return IsConsumerAccount(accessToken).GetValueOrDefault();
		}


		public static async Task<UserType> GetUserType()
		{
			var userType = UserType.None;
			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsLocalUser())
				userType |= UserType.Local;

			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser())
				userType |= UserType.WindowsDomain;

			var scopes = new[] { MicrosoftGraphScope };

			var s = DateTime.UtcNow;
			var token = await GetAccessToken(scopes);
			var d = DateTime.UtcNow.Subtract(s);

			var isMicrosoftUser = token.Token != null;
			if (!isMicrosoftUser)
				return userType;
			JwtSecurityToken securityToken = null;
			try
			{
				var jwtHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
				securityToken = jwtHandler.ReadJwtToken(token.Token);
			}
			catch (Exception)
			{
				return userType;
			}
			// If contains tenant id then acount is Entra ID
			if (securityToken.Claims.Any(c => c.Type == "tid"))
				userType |= UserType.EntraID;

			// For example purposes:
			var issuerClaim = securityToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? "";
			if (issuerClaim.Contains("sts.windows.net"))
				userType |= UserType.MicrosoftBusiness;
			else if (issuerClaim.Contains("login.microsoftonline.com"))
				userType |= UserType.MicrosoftConsumer;

			return userType;
		}

		#region User Profile

		/// <summary>
		/// Get user profile with the access token.
		/// </summary>
		public static UserProfile GetProfile()
		{
			var profile = Global.AppSettings.UserProfiles.FirstOrDefault(p => p.ServiceType == ApiServiceType.Azure);
			return profile;
		}

		public async Task RefreshProfileImage()
		{
			try
			{
				var profile = GetProfile();
				//var credential = await GetTokenCredential();
				var scopes = new[] { MicrosoftGraphScope };
				var accessToken = await GetAccessToken(scopes);
				profile.IsConsumer = IsConsumerAccount(profile.IdToken ?? accessToken.Token);
				profile.Image = await Global.Security.GetProfileImage();
			}
			catch (Exception)
			{
			}
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
			// Please not that access to azure could be denied to consumer accounts from business domain environment.
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
			var accessToken = GetProfile().AccessToken;
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
				var accountId = GetProfile()?.AccountId;
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
					result = await Pca
						.AcquireTokenInteractive(scopes)
						//.WithPrompt(Prompt.SelectAccount)
						.ExecuteAsync();
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
			var profile = GetProfile();
			profile?.Clear();
			if (_account != null)
			{
				await Pca.RemoveAsync(_account);
				_account = null;
			}
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

		/// <summary>
		/// Load this once when app starts.
		/// </summary>
		/// <returns></returns>
		public async Task LoadCurrentAccount()
		{
			EnableTokenCache(Pca.UserTokenCache);
			// Load saved user profile
			var profile = GetProfile();
			if (profile == null)
				return;
			// Returns all the available accounts in the user token cache for the application.
			var accounts = await Pca.GetAccountsAsync();
			if (!accounts.Any())
				return;
			// Attempt to find the account with the saved UserId
			_account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == profile.AccountId);
		}

		public async Task<Microsoft.Graph.Models.User> GetMicrosoftUser(CancellationToken cancellationToken = default)
		{
			var credential = await GetTokenCredential(cancellationToken);
			var client = new GraphServiceClient(credential);
			var ui = await client.Me.GetAsync(cancellationToken: cancellationToken);
			return ui;
		}

		public async Task<BitmapImage> GetProfileImage(CancellationToken cancellationToken = default)
		{
			var credential = await GetTokenCredential(cancellationToken);
			var client = new GraphServiceClient(credential);
			BitmapImage image = null;
			try
			{
				// "https://graph.microsoft.com/v1.0/me/photo/$value"
				var stream = await client.Me.Photo.Content.GetAsync(cancellationToken: cancellationToken);
				image = ConvertToImage(stream);
			}
			catch (Exception)
			{
			}
			return image;
		}

		public async Task<BitmapImage> GetProfileImage(string accessToken, CancellationToken cancellationToken = default)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value", cancellationToken);
				if (!response.IsSuccessStatusCode)
					return null;
				var stream = await response.Content.ReadAsStreamAsync();
				var image = ConvertToImage(stream);
				return image;
			}
		}

		static BitmapImage ConvertToImage(System.IO.Stream stream)
		{
			var bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = stream;
			bitmapImage.EndInit();
			//bitmapImage.Freeze(); // Freeze to make it cross-thread accessible
			return bitmapImage;
		}

		#endregion

		public static async Task<Dictionary<string, string>> GetAzureSubscriptions(TokenCredential credential, CancellationToken cancellationToken = default)
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

		public static async Task<KeyVaultSecret> GetSecretFromKeyVault(
			string keyVaultName, string secretName, string accessToken,
			CancellationToken cancellationToken = default)
		{
			var credential = new AccessTokenCredential(accessToken);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential, cancellationToken);
		}

		public static async Task<KeyVaultSecret> GetSecretFromKeyVault(
			string keyVaultName, string secretName, string tenantId, string clientId, string clientSecret,
			CancellationToken cancellationToken = default)
		{
			var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
			return await GetSecretFromKeyVault(keyVaultName, secretName, credential, cancellationToken);
		}

		public static async Task<KeyVaultSecret> GetSecretFromKeyVault(
			string keyVaultName, string secretName, TokenCredential credential = null,
			CancellationToken cancellationToken = default)
		{
			credential = credential ?? await GetTokenCredential();
			// Azure Key Vault URI
			string kvUri = $"https://{keyVaultName}.vault.azure.net/";
			// Create a new secret client
			var client = new SecretClient(new Uri(kvUri), credential);
			// Retrieve the secret from Azure Key Vault
			var secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
			return secret;
		}

		public static async Task<VaultItem> RefreshVaultItem(Guid? id,
			ObservableCollection<CancellationTokenSource> cancellationTokenSources = default)
		{
			if (id == null || id == Guid.Empty)
				return null;
			var item = Global.AppSettings.VaultItems.FirstOrDefault(x => x.Id == id);
			if (item == null)
				return null;
			var exception = await AppHelper.ExecuteMethod(
			cancellationTokenSources,
			async (cancellationToken) =>
			{
				var secret = await GetSecretFromKeyVault(item.VaultName, item.VaultItemName);
				item.Value = secret?.Value;
				item.ActivationDate = secret?.Properties?.ExpiresOn?.UtcDateTime;
				item.ExpirationDate = secret?.Properties?.NotBefore?.UtcDateTime;
				item.UpdateTimeSettings.LastUpdate = DateTime.UtcNow;
			});
			return item;
		}

		#endregion

		public static bool? IsConsumerAccount(string accessToken, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(accessToken))
				return null;
			var handler = new JwtSecurityTokenHandler();
			var jsonToken = handler.ReadToken(accessToken) as JwtSecurityToken;
			var tid = jsonToken?.Claims.FirstOrDefault(claim => claim.Type == "tid")?.Value;
			return tid == "9188040d-6c67-4c5b-b112-36a304b66dad";
		}

	}
}
