using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System;
using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
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
			await Global.Security.RefreshProfileImage();
		}

		private async void SignOutButton_Click(object sender, RoutedEventArgs e)
		{
			var success = await Global.Security.SignOut();
			LogPanel.Add(
				success
				? "User signed out successfully.\r\n"
				: "No user is currently signed in.\r\n"
				);
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Allows to run mehod once when control is created.
			if (ControlsHelper.AllowLoad(this))
			{
				await Global.Security.LoadCurrentAccount();
				await Global.Security.RefreshProfileImage();
			}
		}


		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			var items = cancellationTokenSources.ToArray();
			foreach (var item in items)
				item.Cancel();
		}

		private async void InspectTokenButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				await Task.Delay(0);
				var scope = new[] { AppSecurityHelper.MicrosoftGraphScope };
				var token = await AppSecurityHelper.GetAccessToken(scope, cancellationToken);
				var accessToken = token.Token;
				var isJwtToken = accessToken.Split('.').Length == 3;
				LogPanel.Add($"Access Token Expiry Date: {token.ExpiresOn}\r\n");
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
					// Optionally, you can print other claims as well
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
			});
		}

		private async void GetSecretWithClientSecretButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var secret = await AppSecurityHelper.GetSecretFromKeyVault(
					KeyVaultNameTextBox.Text, SecretNameTextBox.Text,
					TenantIdTextBox.Text, ClientIdTextBox.Text, ClientSecretPasswordBox.Password,
					cancellationToken);
				LogPanel.Add($"{secret}\r\n");
			});
		}

		private async void GetSecretWithAccessTokenButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var accessToken = AppSecurityHelper.GetProfile().Result?.AccessToken;
				if (string.IsNullOrEmpty(accessToken))
					return;
				var secret = await AppSecurityHelper
				.GetSecretFromKeyVault(KeyVaultNameTextBox.Text, SecretNameTextBox.Text, accessToken, cancellationToken);
				LogPanel.Add($"{secret}\r\n");
			});
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var scope = new[] { AppSecurityHelper.MicrosoftGraphScope };
				var token = await AppSecurityHelper.GetAccessToken(scope, cancellationToken);
				var accessToken = token.Token;
				var contents = await AppSecurityHelper.MakeAuthenticatedApiCall(TestTextBox.Text, accessToken, cancellationToken);
				LogPanel.Add($"{contents}\r\n");
			});
		}

		private async void ListAccountsButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var accounts = await Global.Security.Pca.GetAccountsAsync();
				foreach (var account in accounts)
				{
					LogPanel.Add($"Username: {account.Username}\r\n");
					LogPanel.Add($"HomeAccountId: {account.HomeAccountId}\r\n");
					LogPanel.Add($"Environment: {account.Environment}\r\n");
					LogPanel.Add("\r\n");
				}
			});
		}

		private async void ListSubscriptionsButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				var credential = await AppSecurityHelper.GetTokenCredential(cancellationToken);
				var items = await AppSecurityHelper.GetSubscriptionNamesAndIdsAsync(credential, cancellationToken);
				foreach (var item in items)
					LogPanel.Add($"Subscription: {item.Key} - {item.Value}\r\n");
			});
		}

		#region Control Helper Methods


		/// <summary>
		/// Stores cancellation tokens created on this control that can be stopped with the [Stop] button.
		/// </summary>
		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		/// <summary>
		/// Helps run cancellable methods of this form and logs results to the log panel.
		/// </summary>
		async Task ExecuteMethod(Func<CancellationToken, Task> action)
		{
			LogPanel.Clear();
			var source = new CancellationTokenSource();
			source.CancelAfter(TimeSpan.FromSeconds(30));
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

		#endregion

	}
}
