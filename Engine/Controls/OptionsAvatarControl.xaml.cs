using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Speech;
using JocysCom.VS.AiCompanion.Plugins.Core.TtsMonitor;
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
			Global.VoicesUpdated += Global_VoicesUpdated;
			UpdateAiServices();
			UpdateVoiceLocales();
		}

		private void Tasks_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			if (Global.MainControl.InfoPanel.Tasks.Any())
			{
				AvatarPanel.PlayGlowAnimation();
			}
			else
			{
				AvatarPanel.StopGlowAnimation();
			}
		}

		public AvatarItem Item
		{
			get => Global.AppSettings.AiAvatar;
		}

		#region Update AI Services

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

		#endregion

		#region Update Voice Languages

		public ObservableCollection<KeyValue<string, string>> VoiceLocales { get; set; } = new ObservableCollection<KeyValue<string, string>>();

		private void Global_VoicesUpdated(object sender, EventArgs e)
		{
		}

		public void UpdateVoiceLocales()
		{
			var services = Global.Voices.Items
				.Select(x => (x.Locale, x.LocaleName))
				.Distinct()
				.OrderBy(x => x.LocaleName)
				.Select(x => new KeyValue<string, string>(x.Locale, x.LocaleName))
				.ToList();
			CollectionsHelper.Synchronize(services, VoiceLocales);
		}

		#endregion

		#region Voice Gender

		public ObservableCollection<VoiceGender> Genders { get; }
			= new ObservableCollection<VoiceGender>(new VoiceGender[] { VoiceGender.Male, VoiceGender.Female });

		private async void GenderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateVoiceNames);
		}

		#endregion

		#region Voice Name

		public ObservableCollection<string> VoiceNames { get; set; } = new ObservableCollection<string>();

		private async void VoiceLocalesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateVoiceNames);
		}

		public void UpdateVoiceNames()
		{
			var source = Global.Voices.Items
				.Where(x => x.Locale == Item.VoiceLocale)
				.Where(x => x.Gender == Item.Gender.ToString())
				.Select(x => x.DisplayName)
				.OrderBy(x => x)
				.ToList();
			CollectionsHelper.Synchronize(source, VoiceNames);
		}

		#endregion

		private void AiServicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		SynthesizeClient client;

		bool RecreateClient(VoiceGender? overrideGender, string overrideLocale)
		{
			if (client != null)
				client.Dispose();
			var service = Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == Item.AiServiceId);
			if (service == null)
			{
				LogPanel.Add("Service not found");
				return false;
			}
			// There is no neutral in azure. Use selected.
			if (overrideGender == VoiceGender.Neutral)
				overrideGender = Item.Gender;

			var voices = Global.Voices.Items
				.Where(x =>
					x.Locale == (overrideLocale ?? Item.VoiceLocale) &&
					x.Gender == (overrideGender ?? Item.Gender).ToString()
				)
				// Put favourites at the top.
				.OrderBy(x => x.IsFavorite ? 0 : 1)
				.ThenBy(x => x.DisplayName.Contains("Multilingual") ? 0 : 1)
				.ToList();
			// Try to get voice by name name.
			var voice = voices.FirstOrDefault(x => x.DisplayName == Item.VoiceName);
			// If spoecific vocie not found then probably due to override.
			if (voice == null)
				voice = voices.FirstOrDefault();
			client = new SynthesizeClient(service.ApiSecretKey, service.Region, voice?.ShortName);
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
					await _AI_SpeakSSML(text, Item.Gender, Item.VoiceLocale, null);
				}
				catch (Exception ex)
				{
					LogPanel.Add(ex.ToString() + "\r\n");
				}
			}
			Global.MainControl.InfoPanel.RemoveTask(task);
		}

		public async Task<OperationResult<string>> AI_SpeakSSML(string text, VoiceGender? gender, string language = null, bool? isSsml = null)
		{
			await Task.Delay(0);
			_ = Dispatcher.BeginInvoke(new Action(() =>
			{
				_ = _AI_SpeakSSML(text, gender, language, isSsml);
			}));
			return new OperationResult<string>();
		}

		async Task<OperationResult<string>> _AI_SpeakSSML(string text, VoiceGender? gender, string language = null, bool? isSsml = null)
		{
			try
			{
				if (!RecreateClient(gender, language))
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
				if (RecreateClient(null, null))
				{
					//var names = await client.GetAvailableVoicesAsync();
					//CollectionsHelper.Synchronize(names, Item.VoiceNames);
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

		private void VoiceNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private void This_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
		{
			UpdateMaxSize();
		}

		private void UpdateMaxSize()
		{
			var maxHeight = ActualHeight;
			InstructionsTextBox.MaxHeight = Math.Round(maxHeight * 0.3);
			MessageTextBox.MaxHeight = Math.Round(maxHeight * 0.3);
		}

		private bool HelpInit;

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (MainTabControl.SelectedItem == HelpTabPage && !HelpInit)
			{
				HelpInit = true;
				var bytes = AppHelper.ExtractFile("Documents.zip", "Feature ‐ AI Avatar.rtf");
				ControlsHelper.SetTextFromResource(HelpRichTextBox, bytes);
			}
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Global.MainControl.InfoPanel.Tasks.ListChanged += Tasks_ListChanged;
			}
		}
	}
}
