﻿using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
#if NETFRAMEWORK
#else
using JocysCom.VS.AiCompanion.DataClient;
#endif

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public static class TokenHandler
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

		// Note the // as it's needed for v1 endpoints
		public const string MicrosoftDatabaseScopes = "https://database.windows.net//.default";
		public const string DirectoryReadAllScope = "Directory.Read.All";
		public const string UserReadScope = "User.Read";
		public const string GroupReadAllScope = "Group.Read.All";
		public const string GroupMemberReadAllScope = "GroupMember.Read.All";



		public const string MicrosoftGraphScope = "https://graph.microsoft.com/.default";
		public const string MicrosoftAzureManagementScope = "https://management.azure.com/.default";
		public const string MicrosoftAzureVaultScope = "https://vault.azure.net/.default";
		public const string MicrosoftAzureSqlScope = "https://sql.azuresynapse-dogfood.net/user_impersonation";

		// Fully qualified URI for "User.Read";
		// Permissions to sign in the user and read the user's profile.
		public const string MicrosoftGraphUserReadScope = "https://graph.microsoft.com/User.Read";

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


		private const string CommonAuthority = "https://login.microsoftonline.com/common";
		private const string OrganizationsAuthority = "https://login.microsoftonline.com/organizations";
		private const string ConsumersAuthority = "https://login.microsoftonline.com/consumers";
		private const string TenantAuthority = "https://login.microsoftonline.com/{0}";

		public static IPublicClientApplication Pca
		{
			get
			{
				lock (_PcaLock)
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
						EnableTokenCache(_Pca.UserTokenCache);
#if NETFRAMEWORK
#else
						// Allow SQL helper methods to get database access token.
						SqlInitHelper.GetAzureSqlAccessToken = GetAzureSqlAccessToken;
#endif
					}
					return _Pca;
				}
			}
		}
		static IPublicClientApplication _Pca;
		private static readonly object _PcaLock = new object();


		#region Token Cache

		/// <summary>Store token cache in the app settings.</summary>
		public static void EnableTokenCache(ITokenCache tokenCache)
		{
			tokenCache.SetBeforeAccess(BeforeAccessNotification);
			tokenCache.SetAfterAccess(AfterAccessNotification);
		}

		private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
		{
			args.TokenCache.DeserializeMsalV3(Global.AppSettings.AzureTokenCache);
		}

		private static void AfterAccessNotification(TokenCacheNotificationArgs args)
		{
			// If the access operation resulted in a cache update
			if (args.HasStateChanged)
				Global.AppSettings.AzureTokenCache = args.TokenCache.SerializeMsalV3();
		}

		#endregion


		/// <summary>
		/// Get windows credentials the app is running with.
		/// </summary>
		public static async Task<TokenCredential> GetWinTokenCredentials(bool interactive = false)
		{
			await Task.Delay(0);
			var options = new DefaultAzureCredentialOptions();
			options.ExcludeInteractiveBrowserCredential = !interactive;
			// Default credentials of the Azure environment in which application is running.
			// Credentials currently used to log into Windows.
			var credential = new DefaultAzureCredential(options);
			return credential;
		}

		public static async Task<string> RefreshToken(string[] scopes, bool interactive = false, CancellationToken cancellationToken = default)
		{
			var profile = Global.UserProfile;
			var accessToken = profile.GetToken(scopes);
			// If user is not signed, or token expired.
			var refreshToken = string.IsNullOrEmpty(accessToken) || GetJwtToken(accessToken).ValidTo < DateTime.UtcNow;
			if (refreshToken)
			{
				// Try to silently login which will result in token refresh.
				var result = await MicrosoftResourceManager.Current.SignIn(scopes, interactive, cancellationToken);
				if (!result.Success || result.Data == null)
					return null;
				accessToken = profile.GetToken(scopes);
			}
			return accessToken;
		}

		/// <summary>
		/// Get access token.
		/// </summary>
		/// <param name="scopes">Set of permissions.</param>
		public static async Task<AccessToken> GetAccessToken(string[] scopes, bool interactive = false, CancellationToken cancellationToken = default)
		{
			// Define credentials that will be used to access resources.
			var accessToken = await RefreshToken(scopes, false, cancellationToken);
			var credential = new AccessTokenCredential(accessToken);
			// Add wanted permissions
			var context = new TokenRequestContext(scopes);
			// Get access token.
			try
			{
				var token = await credential.GetTokenAsync(context, cancellationToken);
				return token;
			}
			catch (Exception)
			{
			}
			return default;
		}

		public static Microsoft.IdentityModel.JsonWebTokens.JsonWebToken GetJwtToken(string token)
		{
			var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
			return jwt;
		}

		#region SQL Token

#if NETFRAMEWORK
#else
		public static async Task<AccessToken> GetAzureSqlAccessToken(CancellationToken cancellationToken)
		{
			// Define the scope required for Azure SQL Database
			var scopes = new string[] { MicrosoftAzureSqlScope };
			var token = await GetAccessToken(scopes, interactive: false, cancellationToken);
			return token;
		}
#endif

		#endregion



	}
}
