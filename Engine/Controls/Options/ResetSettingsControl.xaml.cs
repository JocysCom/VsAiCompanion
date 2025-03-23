using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Settings;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
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


		private void FixResetList_Step1(ListInfo target, string defaultValue)
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
				var currentPaths = target.Items.Select(x => x.Key).ToList();
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
				// Add missing items with RestoreIfNotDeleted instruction
				foreach (var path in pathsToInsert)
				{
					// Add a new reset item with RestoreIfNotDeleted instruction
					target.Items.Add(new ListItem
					{
						Key = path,
						Value = defaultValue,
					});
				}
				// Remove items that don't exist in the zip
				foreach (var path in pathsToDelete)
				{
					var existingItem = target.Items.FirstOrDefault(x => x.Key == path);
					if (existingItem != null)
						target.Items.Remove(existingItem);
				}
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error, $"Error synchronizing with settings zip: {ex.Message}");
			}
		}

		/// <summary>
		/// Updates the item states list by comparing current items with their original versions.
		/// </summary>
		/// <param name="statesList">The list that stores item states</param>
		private void FixResetList_Step2(ListInfo statesList)
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
				// Track changes
				int modifiedCount = 0;
				int deletedCount = 0;
				int unchangedCount = 0;

				// Process each item path in the states list
				foreach (var stateItem in statesList.Items.ToList())
				{
					if (!SettingsSourceManager.ParsePathToItem(stateItem.Key, out ItemType itemType, out string itemName))
						continue;
					// Get the current item from the appropriate collection
					var currentItem = Global.GetSettingItem(itemType, itemName);
					// Get the original item from the zip
					var sourceItems = zipItems[itemType];
					var sourceItem = sourceItems?.FirstOrDefault(x => x.Name == itemName);
					// Update the state based on comparison
					if (currentItem == null)
					{
						// Item was deleted by user
						stateItem.Value = ItemState.Deleted.ToString();
						deletedCount++;
					}
					else if (sourceItem != null && IsItemModified(currentItem, sourceItem))
					{
						// Item exists but was modified
						stateItem.Value = ItemState.Modified.ToString();
						modifiedCount++;
					}
					else
					{
						// Item exists and is unchanged
						stateItem.Value = ItemState.None.ToString();
						unchangedCount++;
					}
				}
				// Save changes to the reset list
				Global.Resets.Save();
				// Show results
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Information,
					$"Item states updated: {modifiedCount} modified, {deletedCount} deleted, {unchangedCount} unchanged");
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error,
					$"Error updating item states: {ex.Message}");
			}
		}

		

		/// <summary>
		/// Compares two items to determine if the current item has been modified from the source.
		/// </summary>
		private bool IsItemModified(ISettingsFileItem currentItem, ISettingsFileItem sourceItem)
		{
			// Simple implementation - you may need a more sophisticated comparison
			// based on your specific item types and what constitutes a "modification"

			// Option 1: Use XML serialization to compare
			var currentXml = SerializeToXml(currentItem);
			var sourceXml = SerializeToXml(sourceItem);
			return currentXml != sourceXml;

			// Option 2: If items implement IEquatable or have a good Equals override
			// return !currentItem.Equals(sourceItem);
		}

		/// <summary>
		/// Serializes an item to XML for comparison purposes.
		/// </summary>
		private string SerializeToXml(ISettingsFileItem item)
		{
			try
			{
				var serializer = new System.Xml.Serialization.XmlSerializer(item.GetType());
				using (var sw = new StringWriter())
				{
					serializer.Serialize(sw, item);
					return sw.ToString();
				}
			}
			catch
			{
				// If serialization fails, use a fallback method
				return item.ToString();
			}
		}

		private async void FixResetInstructionsListButton_Click(object sender, RoutedEventArgs e)
		{
			FixResetList_Step1(Global.ResetInstructions, ResetInstruction.RestoreIfNotDeleted.ToString());
			// Save changes to the reset list
			Global.Resets.Save();
			// Refresh the UI
			await ResetInstructionsPanel.BindData(Global.ResetInstructions);
			Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Information, "Fix of Reset Instructions List Complete");
			ResetInstructionsPanel.Sort(true);
		}

		private async void FixResetItemStatesListButton_Click(object sender, RoutedEventArgs e)
		{
			FixResetList_Step1(Global.ResetItemStates, ItemState.None.ToString());
			// Save changes to the reset list
			Global.Resets.Save();
			// Refresh the UI
			await ResetItemStatesPanel.BindData(Global.ResetItemStates);
			Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Information, "Fix of Reset Item State Lists Complete");
			FixResetList_Step2(Global.ResetItemStates);
			ResetInstructionsPanel.Sort(true);
		}

		private void ResetItemStatesToNoneButton_Click(object sender, RoutedEventArgs e)
		{
			var items = Global.ResetItemStates;
			foreach (var item in items.Items)
				item.Value = ItemState.None.ToString();
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
				ResetInstructionsPanel.ValueType = typeof(ResetInstruction);
				await ResetInstructionsPanel.BindData(Global.ResetInstructions);
				ResetItemStatesPanel.ValueType = typeof(ItemState);
				await ResetItemStatesPanel.BindData(Global.ResetItemStates);
				// Move buttons.
				MoveButton(FixButtonsPanel, ResetInstructionsPanel.RightButtonsPanel, FixResetInstructionsListButton);
				MoveButton(FixButtonsPanel, ResetItemStatesPanel.RightButtonsPanel, FixResetItemStatesListButton);
				MoveButton(FixButtonsPanel, ResetItemStatesPanel.RightButtonsPanel, ResetItemStatesToNoneButton);
				// Hide developer panel.
				ResetInstructionsPanel.AddButton.Visibility = Visibility.Collapsed;
				ResetInstructionsPanel.MainDataGrid.CanUserAddRows = false;
				ResetItemStatesPanel.AddButton.Visibility = Visibility.Collapsed;
				ResetItemStatesPanel.MainDataGrid.CanUserAddRows = false;
				DeveloperPanel.Visibility = Visibility.Collapsed;
			}
		}

		private void MoveButton(Panel source, Panel target, Button button)
		{
			source.Children.Remove(button);
			button.Margin = new Thickness(0, 0, 3, 3);
			button.Background = System.Windows.Media.Brushes.Transparent;
			target.Children.Insert(0, button);

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
