using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Settings;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for VaultItemControl.xaml
	/// </summary>
	public partial class ResetSettingsControl : UserControl, INotifyPropertyChanged
	{
		public ResetSettingsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		#region Reset Settings Group Box

		private void ResetAllSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetAllSettings(true);
		}

		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Application Settings"))
				return;
			SettingsSourceManager.ResetAppSettings();
		}

		private void ResetTasksButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Tasks"))
				return;
			SettingsSourceManager.ResetTasks();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Tempaltes"))
				return;
			SettingsSourceManager.ResetTemplates();
		}

		private void ResetPromptsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Prompts"))
				return;
			SettingsSourceManager.ResetPrompts();
			Global.Prompts.Save();
			Global.TriggerPromptingUpdated();
		}

		private void ResetResetsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Resets"))
				return;
			SettingsSourceManager.ResetResets();
			Global.Resets.Save();
		}

		private void ResetVoicesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Avatar Voices"))
				return;
			Global.Voices.ResetToDefault();
			Global.Voices.Save();
			Global.TriggerVoicesUpdated();
		}

		/// <summary>
		/// Use to make screenshots.
		/// </summary>
		private void ResetUIButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetUI();
		}

		private void ResetListsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Lists"))
				return;
			SettingsSourceManager.ResetLists();

		}

		private void ResetEmbeddingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Embeddings"))
				return;
			SettingsSourceManager.ResetEmbeddings();
		}

		private void ResetUiPresetsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("UI Presets"))
				return;
			SettingsSourceManager.ResetUiPresets();
		}

		private void ResetServicesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Services and Models"))
				return;
			SettingsSourceManager.ResetServicesAndModels();
			Global.RaiseOnAiServicesUpdated();
			Global.RaiseOnAiModelsUpdated();
		}

		#endregion

		#region Sync with Settings Zip

		private async void SyncWithSettingsZipButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Get items from zip file
				var zip = SettingsSourceManager.GetSettingsZip();
				if (zip == null)
				{
					Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error, "Settings zip file could not be loaded.");
					return;
				}

				var zipAppDataItems = SettingsSourceManager.GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
				var zipItems = SettingsSourceManager.GetItemsFromZip(zip, zipAppDataItems[0]);

				// Get Tasks and Templates from zip
				var zipTasks = zipItems[ItemType.Task];
				var zipTemplates = zipItems[ItemType.Template];

				var zipPaths = new List<string>();
				zipPaths.AddRange(zipTasks.Select(x => SettingsSourceManager.ConvertItemToPath(ItemType.Task, x.Name)));
				zipPaths.AddRange(zipTemplates.Select(x => SettingsSourceManager.ConvertItemToPath(ItemType.Template, x.Name)));

				var currentPaths = Global.Resets.Items.FirstOrDefault()?.Items.Select(x => x.Key).ToList();

				// Find missing paths
				var pathsToInsert = zipPaths.Except(currentPaths).ToList();
				// Find paths that are in the current list but not in the zip.
				var pathsToDelete = currentPaths.Except(zipPaths).ToList();

				// If no changes needed
				if (!pathsToInsert.Any() && !pathsToDelete.Any())
				{
					Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Information, "All items are synchronized with settings zip file.");
					return;
				}

				// Confirm changes with the user
				var message = $"Found {pathsToInsert.Count} missing items to add and {pathsToDelete.Count} items to remove.\n\nDo you want to proceed?";
				var result = MessageBox.Show(message, "Confirm Synchronization",
					MessageBoxButton.YesNo, MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;

				// Get all reset items from the current list
				var resetList = Global.Resets.Items.FirstOrDefault();
				if (resetList == null)
				{
					resetList = new ListInfo()
					{
						Description = Engine.Resources.MainResources.main_UpdateInstructions_Help,
					};
					Global.Resets.Items.Add(resetList);
				}

				// Add missing items with RestoreIfNotDeleted instruction
				foreach (var path in pathsToInsert)
				{
					// Add a new reset item with RestoreIfNotDeleted instruction
					resetList.Items.Add(new ListItem
					{
						Key = path,
						Value = UpdateInstruction.RestoreIfNotDeleted.ToString()
					});
				}

				// Remove items that don't exist in the zip
				foreach (var path in pathsToDelete)
				{
					var existingItem = resetList.Items.FirstOrDefault(x => x.Key == path);
					if (existingItem != null)
						resetList.Items.Remove(existingItem);
				}

				// Save changes to the reset list
				Global.Resets.Save();

				// Refresh the UI
				await UpdateInstructionsPanel.BindData(resetList);
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Information, "Synchronization Complete");
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error, $"Error synchronizing with settings zip: {ex.Message}");
			}
		}

		#endregion


		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				UpdateInstructionsPanel.ValueType = typeof(UpdateInstruction);
				await UpdateInstructionsPanel.BindData(Global.Resets.Items.FirstOrDefault());
			}
		}

		private void ResetSettingsWithInstructions_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetWithInstructions(true);
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
