using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Engine.Converters;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;

#if NETFRAMEWORK
#else
using JocysCom.VS.AiCompanion.DataClient;
#endif

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	/// <summary>
	/// Class provides functionalities for signing in and out with a Microsoft account
	/// within a .NET desktop application. This class handles token caching and automatic token refreshing, 
	/// ensuring that users do not need to provide credentials and sign in repeatedly to access various resources.
	/// 
	/// Key Features:
	/// - Sign in with your Microsoft account.
	/// - Sign out from your Microsoft account.
	/// - Cache the authentication tokens securely.
	/// - Automatically refresh tokens to maintain access without requiring user intervention.
	/// 
	/// Usage Scenarios:
	/// - Applications that require access to Microsoft resources like OneDrive, Outlook, Azure, etc.
	/// - Scenarios where user experience is improved by avoiding repeated authentication prompts.
	/// 
	/// This class aims to simplify the integration with Microsoft identity services, providing a seamless 
	/// authentication experience in your .NET desktop application.
	/// </summary>
	public class MicrosoftAccountManager
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

		// note the // as it's needed for v1 endpoints
		public const string MicrosoftDatabaseScopes = "https://database.windows.net//.default";
		public const string MicrosoftGraphScope = "https://graph.microsoft.com/.default";
		public const string MicrosoftAzureManagementScope = "https://management.azure.com/.default";
		public const string MicrosoftAzureVaultScope = "https://vault.azure.net/.default";
		public const string MicrosoftAzureSqlScope = "https://sql.azuresynapse-dogfood.net/user_impersonation";
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


		static readonly object _currentLock = new object();
		static MicrosoftAccountManager _Current;
		public static MicrosoftAccountManager Current
		{
			get
			{
				lock (_currentLock)
					return _Current = _Current ?? new MicrosoftAccountManager();
			}
		}

		public IPublicClientApplication Pca
		{
			get
			{
				if (_Pca == null)
				{
					var tenantId = Global.AppSettings?.AppTenantId;
					var builder = PublicClientApplicationBuilder
						.Create(Global.AppSettings?.AppClientId);
					builder = string.IsNullOrEmpty(tenantId)
						? builder.WithAuthority(CommonAuthority)
						: builder.WithAuthority(AzureCloudInstance.AzurePublic, tenantId);
					builder = builder
						.WithRedirectUri("http://localhost")
						.WithDefaultRedirectUri();
					_Pca = builder.Build();
#if NETFRAMEWORK
#else
					SqlInitHelper.GetAccessToken = GetAccessToken;
#endif
				}
				return _Pca;
			}
		}
		IPublicClientApplication _Pca;

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
		public async Task<bool> IsMicrosoftAccount()
		{
			var profile = GetProfile();
			// If access token is empty then application must use
			// user 
			var accessToken = profile.IdToken ?? profile.GetToken(MicrosoftGraphScope);
			TokenCredential credential = await GetTokenCredential();
			return IsConsumerAccount(accessToken).GetValueOrDefault();
		}


		public async Task<UserType> GetUserType()
		{
			var userType = UserType.None;
			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsLocalUser())
				userType |= UserType.Local;

			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser())
				userType |= UserType.WindowsDomain;

			var scopes = new[] { MicrosoftGraphScope };

			var s = DateTime.UtcNow;
			var token = await GetAccessToken(scopes, interactive: false);
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
		public UserProfile GetProfile()
		{
			var profile = Global.AppSettings.UserProfiles.First();
			return profile;
		}

		public async Task RefreshProfileImage(CancellationToken cancellationToken = default)
		{
			var profile = GetProfile();
			var accessToken = profile.GetToken(MicrosoftGraphScope);
			// If user is not signed, return.
			if (string.IsNullOrEmpty(accessToken))
				return;

			var credential = new AccessTokenCredential(accessToken);
			var client = new GraphServiceClient(credential);
			ImageSource image = null;
			try
			{
				var user = await client.Me.GetAsync(cancellationToken: cancellationToken);
				var photoMeta = await client.Me.Photo.GetAsync(cancellationToken: cancellationToken);
				if (photoMeta != null)
				{
					var stream = await client.Me.Photo.Content.GetAsync(cancellationToken: cancellationToken);
					var bitmapImage = ConvertToImage(stream);
					image = bitmapImage;
				}
				else
				{
					image = GetDefaultProfileImage();
				}
			}
			catch (Microsoft.Graph.Models.ODataErrors.ODataError oex)
			{
				if (oex.Error.Code == "ImageNotFound")
					image = GetDefaultProfileImage();
			}
			catch (Exception ex)
			{
				var s = ex.ToString();
			}
			profile.Image = image;
		}

		private void SaveUserProfile(AuthenticationResult result, string[] scopes)
		{
			var account = result.Account;
			var profile = GetProfile();

			// Store access token for specific scope.
			profile.SetToken(result.AccessToken, scopes);

			// Other profile saving information
			profile.ServiceType = ApiServiceType.Azure;
			profile.Username = account.Username;
			profile.AccountId = account.HomeAccountId.Identifier;
			profile.IdToken = result.IdToken;

			// Parse the ID token to extract user claims
			var handler = new JwtSecurityTokenHandler();
			var jsonToken = handler.ReadToken(result.IdToken) as JwtSecurityToken;
			var claims = jsonToken?.Claims;
			profile.Name = claims?.FirstOrDefault(c => c.Type == "name")?.Value;
			// Extract AzureAD/EntraID Directory Tenant ID from the claims.
			profile.TenantId = claims.FirstOrDefault(c => c.Type == "tid")?.Value;
			Global.AppSettings.AppTenantId = profile.TenantId;
		}

		/// <summary>
		/// // By default return credentials that user used to sign in. 
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<TokenCredential> GetTokenCredential(
			bool interactive = false,
			CancellationToken cancellationToken = default)
		{
			// Please not that access to azure could be denied to consumer accounts from business domain environment.
			var credentials = await GetAppTokenCredential(cancellationToken);
			if (credentials != null)
				return credentials;
			return await GetWinTokenCredentials(interactive);
		}

		public async Task<TokenCredential> GetTokenCredential(string[] scopes, bool interactive = false, CancellationToken cancellationToken = default)
		{
			var profile = GetProfile();
			var cachedToken = profile.GetToken(scopes);
			if (!string.IsNullOrEmpty(cachedToken))
			{
				// Use cached token
				return new AccessTokenCredential(cachedToken);
			}
			// Get new token from SignIn if no valid token is found
			var result = await SignIn(scopes, cancellationToken);
			if (!result.Success || result.Data == null)
				return null;
			return new AccessTokenCredential(result.Data.AccessToken);
		}

		public async Task RefreshDatabaseToken()
		{
			var scopes = new string[] { MicrosoftAzureSqlScope };
			var result = await Current.SignIn(scopes); // Auto-interactive sign-in
			if (!result.Success)
			{
				throw new InvalidOperationException("Failed to refresh the token.");
			}
		}

#if NETFRAMEWORK
#else
		private static async Task<AccessToken> GetAccessToken()
		{
			// Define the scope required for Azure SQL Database
			var scopes = new string[] { "https://database.windows.net//.default" };
			try
			{
				// Get the token using existing methods
				var token = await Current.GetAccessToken(scopes, interactive: false);
				// Return the SqlAuthenticationToken
				return token;
			}
			catch (Exception ex)
			{
				// Handle exceptions
				Console.WriteLine($"Error acquiring access token: {ex.Message}");
				return default;
			}
		}
#endif


		/// <summary>
		/// Get the credentials signed into the current app. Get refreshed token if it is expired.
		/// </summary>

		public async Task<TokenCredential> GetAppTokenCredential(CancellationToken cancellationToken = default)
		{
			var accessToken = GetProfile().GetToken(MicrosoftGraphScope);
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
				var result = await SignIn(scopes, cancellationToken);
				if (!result.Success)
					return null;
			}
			var credential = new AccessTokenCredential(accessToken);
			return credential;
		}

		/// <summary>
		/// Get windows credentials the app is running with.
		/// </summary>
		public async Task<TokenCredential> GetWinTokenCredentials(bool interactive = false)
		{
			await Task.Delay(0);
			var options = new DefaultAzureCredentialOptions();
			options.ExcludeInteractiveBrowserCredential = !interactive;
			// Default credentials of the Azure environment in which application is running.
			// Credentials currently used to log into Windows.
			var credential = new DefaultAzureCredential(options);
			return credential;
		}

		#endregion

		#region SignIn and SignOut

		public async Task<OperationResult<AuthenticationResult>> SignIn(
			string[] scopes, CancellationToken cancellationToken = default)
		{
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
				var account = await GetCurrentAccount();
				if (account == null)
				{
					requiresUI = true;
				}
				else
				{
					// Get result that includes access tokens that grant the application the rights to fetch user information.
					result = await Pca.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
				}
			}
			catch (MsalUiRequiredException ex)
			{
				if (!string.IsNullOrEmpty(ex.Claims))
				{
					try
					{
						var builder = Pca.AcquireTokenInteractive(scopes);
						if (!string.IsNullOrEmpty(ex.Claims))
							builder = builder.WithClaims(ex.Claims);
						result = await builder.ExecuteAsync(cancellationToken);
					}
					catch (Exception innerEx)
					{
						return new OperationResult<AuthenticationResult>(innerEx);
					}
				}
				else
				{
					requiresUI = true;
				}

			}
			catch (Exception ex)
			{
				return new OperationResult<AuthenticationResult>(ex);
			}
			if (requiresUI)
			{
				try
				{
					// No token in the cache, attempt to acquire a token using Windows Integrated Authentication
					//var builder = Pca.AcquireTokenByIntegratedWindowsAuth(scopes);
					// Multi-factor authentication (MFA) is a multi-step account login process that requires users to enter more information than just a password. 
					var builder = Pca.AcquireTokenInteractive(scopes);
					result = await builder.ExecuteAsync(cancellationToken);
				}
				catch (Exception ex)
				{
					return new OperationResult<AuthenticationResult>(ex);
				}
			}
			SaveUserProfile(result, scopes);
			return new OperationResult<AuthenticationResult>(result);
		}

		public async Task<bool> SignOut()
		{
			var account = await GetCurrentAccount();
			if (account != null)
				await Pca.RemoveAsync(account);
			var profile = GetProfile();
			profile.Clear();
			return true;
		}

		#endregion

		#region Authentication

		/// <summary>
		/// Get access token.
		/// </summary>
		/// <param name="scopes">Set of permissions.</param>
		public async Task<AccessToken> GetAccessToken(string[] scopes, bool interactive = false, CancellationToken cancellationToken = default)
		{
			// Define credentials that will be used to access resources.
			var credential = await GetTokenCredential(interactive, cancellationToken);
			// Add wanted permissions
			var context = new TokenRequestContext(scopes);
			// Get access token.
			try
			{
				var token = await credential.GetTokenAsync(context, cancellationToken);
				//if (token.ExpiresOn <= DateTimeOffset.UtcNow)
				//{
				//	// Refresh the token
				//	var result = await SignIn(scopes, cancellationToken);
				//	if (result.Success)
				//		token = await credential.GetTokenAsync(context, cancellationToken);
				//}
				return token;
			}
			catch (Exception)
			{
			}
			return default;
		}

		/// <summary>
		/// Load this once when app starts.
		/// </summary>
		/// <returns></returns>
		public async Task<IAccount> GetCurrentAccount()
		{
			EnableTokenCache(Pca.UserTokenCache);
			// Load saved user profile
			var profile = GetProfile();
			if (string.IsNullOrEmpty(profile.AccountId))
				return null;
			// Returns all the available accounts in the user token cache for the application.
			var accounts = await Pca.GetAccountsAsync();
			// Attempt to find the account with the saved UserId
			var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == profile.AccountId);
			return account;
		}

		public async Task<Microsoft.Graph.Models.User> GetMicrosoftUser(CancellationToken cancellationToken = default)
		{
			var profile = GetProfile();
			var accessToken = profile.GetToken(MicrosoftGraphScope);
			// If user is not signed, return.
			if (string.IsNullOrEmpty(accessToken))
				return null;

			var credential = new AccessTokenCredential(accessToken);
			var client = new GraphServiceClient(credential);
			var ui = await client.Me.GetAsync(cancellationToken: cancellationToken);
			return ui;
		}

		public ImageSource GetDefaultProfileImage()
		{
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_user_azure.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			var drawingImage = SvgHelper.LoadSvgFromString(contents);
			var bounds = drawingImage.Drawing.Bounds;
			// Add 10% top padding.
			var topPadding = bounds.Height * 0.10;
			var geometry = new RectangleGeometry(new System.Windows.Rect(0, 0, bounds.Width, bounds.Height + topPadding));
			var translatedDrawing = new GeometryDrawing(Brushes.Transparent, null, geometry);
			var imageDrawing = new DrawingGroup();
			imageDrawing.Children.Add(drawingImage.Drawing);
			imageDrawing.Transform = new TranslateTransform(0, topPadding);
			var drawingGroup = new DrawingGroup();
			drawingGroup.Children.Add(translatedDrawing);
			drawingGroup.Children.Add(imageDrawing);
			return new DrawingImage(drawingGroup);
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

		public async Task<Dictionary<string, string>> GetAzureSubscriptions(CancellationToken cancellationToken = default)
		{
			var scopes = new[] { MicrosoftAzureManagementScope };
			var credential = await GetTokenCredential(scopes, cancellationToken: cancellationToken);

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

		public async Task<string> MakeAuthenticatedApiCall(
			string url, string accessToken,
			CancellationToken cancellationToken = default)
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var response = await httpClient.GetAsync(url, cancellationToken);
				//var phrase = response.ReasonPhrase;
				var contents = await response.Content.ReadAsStringAsync();
				return contents;
			}
		}

		public async Task<string> MakeAuthenticatedCall(string url, CancellationToken cancellationToken = default)
		{
			// Define the scope required to access the target resource
			// This is a common example, you should replace it with the correct scope for your resource
			var scopes = new string[] { $"{url}/.default" };
			var token = await GetAccessToken(scopes, interactive: true, cancellationToken);
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

		public async Task<KeyVaultSecret> GetSecretFromKeyVault(string keyVaultName, string secretName, CancellationToken cancellationToken = default)
		{
			var scopes = new[] { MicrosoftAzureVaultScope };
			var credential = await GetTokenCredential(scopes, cancellationToken: cancellationToken);
			// Azure Key Vault URI
			string kvUri = $"https://{keyVaultName}.vault.azure.net/";
			// Create a new secret client
			var client = new SecretClient(new Uri(kvUri), credential);
			// Retrieve the secret from Azure Key Vault
			var secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
			return secret;
		}

		public async Task<VaultItem> RefreshVaultItem(Guid? id,
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
				JocysCom.ClassLibrary.Controls.ControlsHelper.AppInvoke(() =>
				{
					item.Value = secret?.Value;
					item.ActivationDate = secret?.Properties?.NotBefore?.UtcDateTime;
					item.ExpirationDate = secret?.Properties?.ExpiresOn?.UtcDateTime;
					item.UpdateTimeSettings.LastUpdate = DateTime.UtcNow;
				});
			});
			return item;
		}

		public async Task<string> CheckAndGet(Guid? vaultItemId, string defaultSecret,
			ObservableCollection<CancellationTokenSource> cancellationTokenSources = default)
		{
			if (vaultItemId == null)
				return defaultSecret;
			var item = Global.AppSettings.VaultItems.FirstOrDefault(x => x.Id == vaultItemId.Value);
			if (item == null)
				return null;
			var update = item.ShouldCheckForUpdates();
			if (update)
				await RefreshVaultItem(item.Id, cancellationTokenSources);
			return item.Value;
		}

		#endregion

		public bool? IsConsumerAccount(string accessToken, CancellationToken cancellationToken = default)
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
