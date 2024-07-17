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
	public class MicrosoftResourceManager
	{

		static readonly object _currentLock = new object();
		static MicrosoftResourceManager _Current;
		public static MicrosoftResourceManager Current
		{
			get
			{
				lock (_currentLock)
					return _Current = _Current ?? new MicrosoftResourceManager();
			}
		}


		public async Task<UserType> GetUserType()
		{
			var userType = UserType.None;
			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsLocalUser())
				userType |= UserType.Local;

			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser())
				userType |= UserType.WindowsDomain;

			var s = DateTime.UtcNow;
			var token = await TokenHandler.GetAccessToken(new[] { TokenHandler.MicrosoftGraphScope }, interactive: false);
			var d = DateTime.UtcNow.Subtract(s);

			// If cloud token is null then user is Microsoft Windows user.
			if (token.Token == null)
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

		public async Task RefreshProfileImage(CancellationToken cancellationToken = default)
		{
			var scopes = new string[] { TokenHandler.MicrosoftGraphScope };
			var accessToken = await TokenHandler.RefreshToken(scopes, false, cancellationToken);
			if (string.IsNullOrEmpty(accessToken))
				return;
			var credential = new AccessTokenCredential(accessToken);
			var client = new GraphServiceClient(credential);
			ImageSource image = null;
			try
			{
				var user = await client.Me.GetAsync(cancellationToken: cancellationToken);
				var photoMeta = await client.Me.Photo.GetAsync(cancellationToken: cancellationToken);
				if (photoMeta == null)
				{
					image = GetDefaultProfileImage();
				}
				else
				{
					var stream = await client.Me.Photo.Content.GetAsync(cancellationToken: cancellationToken);
					var bitmapImage = ConvertToImage(stream);
					image = bitmapImage;
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
			Global.UserProfile.Image = image;
		}

		private void SaveUserProfile(AuthenticationResult result, string[] scopes)
		{
			var account = result.Account;
			var profile = Global.UserProfile;

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


		#endregion


		#region SignIn and SignOut

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


		public async Task<bool> SignOut()
		{
			Global.UserProfile.Clear();
			var accounts = await TokenHandler.Pca.GetAccountsAsync();
			foreach (var account in accounts)
				await TokenHandler.Pca.RemoveAsync(account);
			return true;
		}


		/// <summary>
		/// Acquire access token silently or interactive.
		/// </summary>
		public async Task<OperationResult<AuthenticationResult>> SignIn(
			string[] scopes,
			bool interactive = false,
			CancellationToken cancellationToken = default)
		{
			AuthenticationResult result = null;
			string claims = null;
			try
			{
				// Returns all the available accounts in the user token cache for the application.
				var accounts = await TokenHandler.Pca.GetAccountsAsync();
				var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == Global.UserProfile.AccountId);
				// If last saved account was not found then...
				if (account != null)
					// Get result that includes access tokens that grant the application the rights to fetch user information.
					result = await TokenHandler.Pca.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
			}
			catch (MsalUiRequiredException ex)
			{
				claims = ex.Claims;
			}
			catch (Exception ex)
			{
				return new OperationResult<AuthenticationResult>(ex);
			}
			// If failed to retrieve token silently and iteractive retrieval is disbled then...
			if (result == null && !interactive)
				return new OperationResult<AuthenticationResult>(result);
			// If failed to retrieve token silently then...
			if (result == null)
			{
				try
				{
					// Try to acquire token interactive.
					var builder = TokenHandler.Pca.AcquireTokenInteractive(scopes);
					if (!string.IsNullOrEmpty(claims))
						builder = builder.WithClaims(claims);
					result = await builder.ExecuteAsync(cancellationToken);
				}
				catch (Exception ex)
				{
					return new OperationResult<AuthenticationResult>(ex);
				}
			}
			SaveUserProfile(result, scopes);
			Global.UserProfile.SetToken(result.AccessToken, scopes);
			return new OperationResult<AuthenticationResult>(result);
		}

		#endregion

		#region Authentication

		public async Task<Microsoft.Graph.Models.User> GetMicrosoftUser(CancellationToken cancellationToken = default)
		{
			var scopes = new string[] { TokenHandler.MicrosoftGraphScope };
			var accessToken = await TokenHandler.RefreshToken(scopes, false, cancellationToken);
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

		#region Microsoft Resources


		/// <summary>
		/// Determine if it is a Microsoft account by checking if the access token has a specific tenant ID for Microsoft accounts.
		/// </summary>
		public bool IsMicrosoftAccount()
		{
			var profile = Global.UserProfile;
			// If access token is empty then application must use user.
			var accessToken = profile.IdToken ?? profile.GetToken(TokenHandler.MicrosoftGraphScope);
			return IsConsumerAccount(accessToken).GetValueOrDefault();
		}

		public bool? IsConsumerAccount(string accessToken, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(accessToken))
				return null;
			var handler = new JwtSecurityTokenHandler();
			var jsonToken = handler.ReadToken(accessToken) as JwtSecurityToken;
			var tid = jsonToken?.Claims.FirstOrDefault(claim => claim.Type == "tid")?.Value;
			return tid == "9188040d-6c67-4c5b-b112-36a304b66dad";
		}

		public async Task<Dictionary<string, string>> GetAzureSubscriptions(CancellationToken cancellationToken = default)
		{
			// Dictionary to store subscription names and IDs
			var subscriptionsDict = new Dictionary<string, string>();


			var scopes = new[] { TokenHandler.MicrosoftAzureManagementScope };
			var accessToken = await TokenHandler.RefreshToken(scopes, false, cancellationToken);
			if (string.IsNullOrEmpty(accessToken))
				return subscriptionsDict;
			var credential = new AccessTokenCredential(accessToken);

			// Initialize the ArmClient
			var armClient = new ArmClient(credential);

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
			var token = await TokenHandler.GetAccessToken(scopes, interactive: true, cancellationToken);
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

		#region Key Vaults

		public async Task<KeyVaultSecret> GetSecretFromKeyVault(string keyVaultName, string secretName, CancellationToken cancellationToken = default)
		{
			var scopes = new[] { TokenHandler.MicrosoftAzureVaultScope };
			var credential = await TokenHandler.GetTokenCredential(scopes, cancellationToken: cancellationToken);
			// Azure Key Vault URI
			string kvUri = $"https://{keyVaultName}.vault.azure.net/";
			// Create a new secret client
			var client = new SecretClient(new Uri(kvUri), credential);
			// Retrieve the secret from Azure Key Vault
			var secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
			return secret;
		}

		public async Task<VaultItem> RefreshItemFromKeyVaultSecret(Guid? id,
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

		public async Task<string> GetKeyVaultSecretValue(Guid? vaultItemId, string defaultSecret,
			ObservableCollection<CancellationTokenSource> cancellationTokenSources = default)
		{
			if (vaultItemId == null)
				return defaultSecret;
			var item = Global.AppSettings.VaultItems.FirstOrDefault(x => x.Id == vaultItemId.Value);
			if (item == null)
				return null;
			var update = item.ShouldCheckForUpdates();
			if (update)
				await RefreshItemFromKeyVaultSecret(item.Id, cancellationTokenSources);
			return item.Value;
		}

		#endregion

		#region Microsoft Graph

		/// <summary>
		/// Retrieve Azure groups the user is a member of (direct or indirect).
		/// </summary>
		public async Task<List<string>> GetUserAzureGroups(CancellationToken cancellationToken = default)
		{
			var groups = new List<string>();

			// Get the access token and initialize the GraphServiceClient
			var scopes = new[] { TokenHandler.GroupMemberReadAllScope, TokenHandler.UserReadScope }; // Ensure we are using valid scope
			var accessToken = await TokenHandler.RefreshToken(scopes, true, cancellationToken);
			var credential = new AccessTokenCredential(accessToken);
			if (credential == null)
			{
				return groups;
			}

			var client = new GraphServiceClient(credential);

			try
			{
				// Create request body for the GetMemberGroups call
				var body = new Microsoft.Graph.Me.GetMemberGroups.GetMemberGroupsPostRequestBody
				{
					SecurityEnabledOnly = false
				};

				// Retrieve the user's group memberships from the Microsoft Graph API
				var userGroupsResponse = await client.Me
					.GetMemberGroups
					.PostAsGetMemberGroupsPostResponseAsync(body, cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				var groupIds = userGroupsResponse.Value;
				if (groupIds?.Any() == true)
				{
					// Retrieve group details
					foreach (var groupId in groupIds)
					{
						try
						{
							var group = await client.Groups[groupId]
								.GetAsync(cancellationToken: cancellationToken)
								.ConfigureAwait(false);

							if (group != null)
								groups.Add(group.DisplayName);
						}
						catch (ServiceException ex)
						{
							System.Diagnostics.Debug.WriteLine($"Graph API error when retrieving group details for group {groupId}: {ex.Message}");
						}
					}
				}
			}
			catch (ServiceException ex)
			{
				// Handle any Graph API specific errors here
				System.Diagnostics.Debug.WriteLine($"Graph API error: {ex.Message}");
			}

			return groups;
		}




		#endregion

	}
}
