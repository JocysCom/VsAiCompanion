using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Mcp;
using JocysCom.VS.AiCompanion.Engine.Mcp.Models;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for McpServerListControl.xaml
	/// </summary>
	public partial class McpServerListControl : UserControl, INotifyPropertyChanged
	{
		public McpServerListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.McpServers.ListChanged += McpServers_ListChanged;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			Global.UserProfile.PropertyChanged += profile_PropertyChanged;
		}

		public ObservableCollection<McpServerConfig> CurrentItems { get; set; } = new ObservableCollection<McpServerConfig>();

		private async void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.MaxRiskLevel) ||
				e.PropertyName == nameof(AppData.MaxRiskLevelWhenSignedOut)
				)
			{
				var view = (ICollectionView)MainItemsControl.ItemsSource;
				await Helper.Debounce(view.Refresh);
			}
		}

		private async void profile_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (
				e.PropertyName == nameof(UserProfile.IsSignedIn) ||
				e.PropertyName == nameof(UserProfile.UserGroups))
			{
				var view = (ICollectionView)MainItemsControl.ItemsSource;
				await Helper.Debounce(view.Refresh);
			}
		}

		private async void McpServers_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			await Helper.Debounce(UpdateOnListChanged, AppHelper.NavigateDelayMs);
		}

		public IList<McpServerConfig> GetAllServers()
		{
			var servers = Global.AppSettings.McpServers
				.ToList();
			return servers;
		}

		public void UpdateOnListChanged()
		{
			var servers = GetAllServers();
			servers = servers
				.OrderBy(x => x.MaxRiskLevel)
				.ThenBy(x => x.ServerId)
				.ToArray();

			ClassDescription = "Model Context Protocol (MCP) servers provide tools and resources that can be used by AI assistants.";
			OnPropertyChanged(nameof(ClassDescription));

			ClassLibrary.Collections.CollectionsHelper.Synchronize(servers, CurrentItems, new McpServerConfigComparer());
		}

		public string ClassDescription { get; set; }

		class McpServerConfigComparer : IEqualityComparer<McpServerConfig>
		{
			public bool Equals(McpServerConfig x, McpServerConfig y) => x.ServerId == y.ServerId;
			public int GetHashCode(McpServerConfig obj) => obj.ServerId?.GetHashCode() ?? 0;
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Reload MCP server configurations from discovery locations
				var loader = new McpConfigurationLoader();
				var discoveredConfigurations = await loader.DiscoverConfigurationsAsync();

				// Update the global settings
				var currentIds = Global.AppSettings.McpServers.Select(x => x.ServerId).ToHashSet();

				foreach (var kvp in discoveredConfigurations)
				{
					var filePath = kvp.Key;
					var mcpConfig = kvp.Value;

					// Convert each server in the MCP configuration to our format
					foreach (var serverKvp in mcpConfig.AllServers)
					{
						var serverId = serverKvp.Key;
						var serverDef = serverKvp.Value;

						if (!currentIds.Contains(serverId))
						{
							var mcpServerConfig = new McpServerConfig
							{
								ServerId = serverId,
								ServerDefinition = serverDef,
								AutoStart = !serverDef.Disabled,
								AutoRestart = true,
								MaxRiskLevel = RiskLevel.Medium,
								ApprovalProcess = ToolCallApprovalProcess.User,
								Disabled = serverDef.Disabled
							};

							Global.AppSettings.McpServers.Add(mcpServerConfig);
						}
					}
				}

				UpdateOnListChanged();
			}
			catch (System.Exception ex)
			{
				MessageBox.Show($"Error refreshing MCP servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void DisableAllButton_Click(object sender, RoutedEventArgs e)
		{
			var servers = GetAllServers();
			foreach (var server in servers)
				server.Disabled = true;
		}

		private void EnableLowRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Low);

		private void EnableMediumRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Medium);

		private void EnableHighRiskButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.High);

		private void EnableAllButton_Click(object sender, RoutedEventArgs e)
			=> EnableUpTo(RiskLevel.Critical);

		void EnableUpTo(RiskLevel maxRiskLevel)
		{
			var servers = GetAllServers();
			foreach (var server in servers)
				if (server.MaxRiskLevel >= RiskLevel.None && server.MaxRiskLevel <= maxRiskLevel)
					server.Disabled = false;
		}

		private void AddServerButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var newServer = new McpServerConfig
				{
					ServerId = "new-server",
					ServerDefinition = new McpServerDefinition
					{
						Command = "npx",
						Args = new[] { "@modelcontextprotocol/server-filesystem", "/path/to/directory" }
					},
					AutoStart = false,
					AutoRestart = true,
					MaxRiskLevel = RiskLevel.Medium,
					ApprovalProcess = ToolCallApprovalProcess.User
				};

				Global.AppSettings.McpServers.Add(newServer);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show($"Error adding MCP server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Name = "McpServersPanel";
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				UpdateOnListChanged();
			}
		}

		#region Copy and Save

		System.Windows.Forms.SaveFileDialog ExportSaveFileDialog { get; } = new System.Windows.Forms.SaveFileDialog();

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var servers = GetAllServers().Where(x => !x.Disabled).ToList();
				var text = Client.Serialize(servers, true);
				AppHelper.SetClipboard(text);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show($"Error copying MCP servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void SaveAsButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var dialog = ExportSaveFileDialog;
				dialog.DefaultExt = "*.json";
				dialog.FileName = $"{JocysCom.ClassLibrary.Configuration.AssemblyInfo.Entry.Product} MCP Servers.json".Replace(" ", "_");
				dialog.FilterIndex = 1;
				dialog.RestoreDirectory = true;
				dialog.Title = "Save MCP Server Configuration";
				DialogHelper.AddFilter(dialog, ".json");
				DialogHelper.AddFilter(dialog);
				var result = dialog.ShowDialog();
				if (result != System.Windows.Forms.DialogResult.OK)
					return;

				var servers = GetAllServers().Where(x => !x.Disabled).ToList();
				var text = Client.Serialize(servers, true);
				if (string.IsNullOrEmpty(text))
					return;
				var bytes = System.Text.Encoding.UTF8.GetBytes(text);
				JocysCom.ClassLibrary.Configuration.SettingsHelper.WriteIfDifferent(dialog.FileName, bytes);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show($"Error saving MCP servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		#endregion
	}
}
