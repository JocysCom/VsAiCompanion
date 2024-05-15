using JocysCom.ClassLibrary.Controls;
using Microsoft.Identity.Client;
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
			string[] scopes = new string[] { "User.Read" };
			AuthenticationResult result;
			try
			{
				_account = (await _pca.GetAccountsAsync()).FirstOrDefault();
				result = await _pca.AcquireTokenSilent(scopes, _account).ExecuteAsync();
			}
			catch (MsalUiRequiredException)
			{
				result = await _pca.AcquireTokenInteractive(scopes).ExecuteAsync();
			}

			_account = result.Account;

			UserName.Text = result.Account.Username;
			await LoadUserAvatar(result.AccessToken);
		}

		private async Task LoadUserAvatar(string accessToken)
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
					UserAvatar.Source = bitmap;
				}
			}
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				_pca = PublicClientApplicationBuilder.Create(Global.AppSettings?.ClientAppId)
					.WithRedirectUri("http://localhost")
					.WithDefaultRedirectUri()
					.Build();
			}
		}
	}

}
