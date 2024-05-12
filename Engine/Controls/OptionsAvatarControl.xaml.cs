using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Speech;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for OptionsAvatarControl.xaml
	/// </summary>
	public partial class OptionsAvatarControl : UserControl
	{
		public OptionsAvatarControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.OnAiServicesUpdated += Global_OnAiServicesUpdated;
			UpdateAiServices();
		}

		public AvatarItem Item
		{
			get => Global.AppSettings.AiAvatar;
		}

		private void Global_OnAiServicesUpdated(object sender, System.EventArgs e)
			=> UpdateAiServices();

		public ObservableCollection<AiService> AiServices { get; set; } = new ObservableCollection<AiService>();

		public void UpdateAiServices()
		{
			var services = Global.AppSettings.AiServices
				.Where(x => x.ServiceType == ApiServiceType.Azure)
				.ToList();
			CollectionsHelper.Synchronize(services, AiServices);
		}

		private void AiServicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		SynthesizeClient client;

		bool CheckClient()
		{
			if (client != null)
				client.Dispose();
			var service = Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == Item.AiServiceId);
			if (service == null)
			{
				LogPanel.Add("Service not found");
				return false;
			}
			client = new SynthesizeClient(service.ApiSecretKey, service.Region, Item.VoiceName);
			return true;
		}

		private async void PlayButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var task = new object();
			Global.MainControl.InfoPanel.AddTask(task);
			LogPanel.Clear();
			var text = MessageTextBox.Text?.Trim();
			if (string.IsNullOrEmpty(text))
			{
				LogPanel.Add("Message is empty!");
			}
			else
			{
				try
				{
					await _AI_SpeakSSML(text, false);
				}
				catch (Exception ex)
				{
					LogPanel.Add(ex.ToString() + "\r\n");
				}
			}
			Global.MainControl.InfoPanel.RemoveTask(task);
		}

		public async Task<OperationResult<string>> AI_SpeakSSML(string text, bool isSsml)
		{
			await Task.Delay(0);
			_ = Dispatcher.BeginInvoke(new Action(() =>
			{
				_ = _AI_SpeakSSML(text, isSsml);
			}));
			return new OperationResult<string>();
		}

		async Task<OperationResult<string>> _AI_SpeakSSML(string text, bool isSsml)
		{
			try
			{
				if (!CheckClient())
					return new OperationResult<string>(new Exception("AI Avatar cofiguration is not valid."));
				await client.Synthesize(text, isSsml, Item.CacheAudioData);
				var jsonOptions = new JsonSerializerOptions() { WriteIndented = false };
				var json = System.Text.Json.JsonSerializer.Serialize(client.AudioInfo, jsonOptions);
				Dispatcher.Invoke(() =>
				{
					LogPanel.Add(client.AudioFilePath + "\r\n");
					LogPanel.Add(client.AudioInfoPath + "\r\n");
					LogPanel.Add("\r\n");
					LogPanel.Add(json);
					AvatarPanel.Play(client.AudioFilePath, client.AudioInfo);
				});
				return new OperationResult<string>();
			}
			catch (Exception ex)
			{
				Dispatcher.Invoke(() =>
				{
					LogPanel.Add(ex.ToString() + "\r\n");
				});
				return new OperationResult<string>(ex);
			}
		}

		private void StopButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			client?.Stop();
			AvatarPanel.AnimationAndMediaStop();
		}

		private async void VoiceNamesRefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var task = new object();
			Global.MainControl.InfoPanel.AddTask(task);
			try
			{
				if (CheckClient())
				{
					var names = await client.GetAvailableVoicesAsync();
					CollectionsHelper.Synchronize(names, Item.VoiceNames);
					var details = await client.GetAvailableVoicesWithDetailsAsync();
					CollectionsHelper.Synchronize(details, Global.Voices.Items);
				}
			}
			catch (Exception ex)
			{
				LogPanel.Add(ex.ToString() + "\r\n");
			}
			Global.MainControl.InfoPanel.RemoveTask(task);
		}

		//private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//	//	AvatarPanel.Visibility = Global.AppSettings.ShowAvatar
		//	//? Visibility.Visible
		//	//: Visibility.Collapsed;

		//	// Global.AppSettings.ShowAvatar = !Global.AppSettings.ShowAvatar;
		//}
	}
}
