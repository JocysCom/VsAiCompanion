using JocysCom.ClassLibrary;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public class AppSecurityHelper
	{

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


		private void SaveUserProfile(AuthenticationResult result)
		{
			var account = result.Account;
			// Save User profile.
			var profile = Global.AppSettings.UserProfiles
				.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure && x.Username == account.Username);
			if (profile == null)
			{
				profile = new UserProfile();
				Global.AppSettings.UserProfiles.Add(profile);
			}
			profile.ServiceType = ApiServiceType.Azure;
			profile.Username = account.Username;
			profile.AccountId = account.HomeAccountId.Identifier;
			profile.AccessToken = result.AccessToken;
			// Profile will be saved when app close.
		}


		#endregion

		#region Authentication

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

		public IAccount _account;

		public async Task<OperationResult<bool>> SignIn()
		{
			var scopes = new string[] { "User.Read" };
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
				return new OperationResult<bool>(ex);
			}
			if (requiresUI)
			{
				try
				{
					result = await Pca.AcquireTokenInteractive(scopes).ExecuteAsync();
				}
				catch (Exception ex)
				{
					return new OperationResult<bool>(ex);
				}
			}
			_account = result?.Account;
			SaveUserProfile(result);
			return new OperationResult<bool>(true);
		}

		public async Task<OperationResult<IAccount>> LoadCurrentAccount()
		{
			// Load saved user profile
			var profileResult = GetProfile();
			if (!profileResult.Success)
				return new OperationResult<IAccount>(profileResult.Errors.Select(x => new Exception(x)));
			// Load application azure account from this profile.
			var profile = profileResult.Result;
			EnableTokenCache(Pca.UserTokenCache);
			// Returns all the available accounts in the user token cache for the application.
			var accounts = await Pca.GetAccountsAsync();
			if (!accounts.Any())
				return new OperationResult<IAccount>(new Exception("No cached account found. Please sign in."));
			// Attempt to find the account with the saved UserId
			_account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == profile.AccountId);
			return new OperationResult<IAccount>(_account);
		}

		public async Task<bool> SignOut()
		{
			if (_account == null)
				return false;
			await Pca.RemoveAsync(_account);
			_account = null;
			var profile = GetProfile();
			if (profile != null)
				Global.AppSettings.UserProfiles.Remove(profile.Result);
			return true;
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

	}
}
