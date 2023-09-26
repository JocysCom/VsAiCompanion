using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PromptsControl.xaml
	/// </summary>
	public partial class PromptsControl : UserControl, INotifyPropertyChanged
	{
		public PromptsControl()
		{
			var names = Global.PromptItems.Items.Select(x => x.Name).OrderBy(x => x).ToList();
			PromptNames = new BindingList<string>(names);
			PromptOptions = new BindingList<string>();
			InitializeComponent();
		}

		TemplateItem Item;

		public void BindData(TemplateItem item = null)
		{
			if (Equals(item, Item))
				return;
			PromptOptionComboBox.SelectionChanged -= PromptOptionComboBox_SelectionChanged;
			PromptNameComboBox.SelectionChanged -= PromptNameComboBox_SelectionChanged;
			DataContext = null;
			FixPromptName(item);
			FixPromptOption(item);
			SetPattern(item.PromptName);
			SetOptions(item.PromptName);
			Item = item;
			DataContext = item;
			PromptNameComboBox.SelectionChanged += PromptNameComboBox_SelectionChanged;
			PromptOptionComboBox.SelectionChanged += PromptOptionComboBox_SelectionChanged;
		}

		void FixPromptName(TemplateItem item)
		{
			if (!PromptNames.Contains(item.PromptName))
				item.PromptName = PromptNames.FirstOrDefault();
		}

		void FixPromptOption(TemplateItem item)
		{
			var options = Global.PromptItems.Items
				.FirstOrDefault(x => x.Name == item.PromptName)?
				.Options.OrderBy(x => x).ToList();
			// If item is not in the list then...
			if (!options.Contains(item.PromptOption))
				// Set default value.
				item.PromptOption = options.FirstOrDefault();
		}

		void SetPattern(string promptName)
		{
			var prompt = Global.PromptItems.Items.FirstOrDefault(x => x.Name == promptName)
				?? Global.PromptItems.Items.FirstOrDefault();
			if (prompt == null)
			{
				PatternStartLabel.Content = "";
				PatternEndLabel.Content = "";
				CollectionsHelper.Synchronize(Array.Empty<string>(), PromptOptions);
				return;
			}
			var parts = prompt.Pattern.Split(new string[] { "{0}" }, StringSplitOptions.None);
			if (parts.Length != 2)
				return;
			PatternStartLabel.Content = parts[0];
			var part1 = parts[1].TrimEnd(' ', '.');
			PatternEndLabel.Content = part1;
			PatternEndLabel.Visibility = string.IsNullOrEmpty(part1)
				? System.Windows.Visibility.Collapsed
				: System.Windows.Visibility.Visible;
		}

		void SetOptions(string promptName)
		{
			var options = Global.PromptItems.Items
				.FirstOrDefault(x => x.Name == promptName)?
				.Options.OrderBy(x => x).ToList();
			CollectionsHelper.Synchronize(options, PromptOptions);
		}

		private void PromptNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Set options.
			var promptName = e.AddedItems.Cast<string>().FirstOrDefault();
			SetPattern(promptName);
			SetOptions(promptName);
			FixPromptOption(Item);
		}

		private void PromptOptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		public BindingList<string> PromptNames { get; set; }

		public BindingList<string> PromptOptions { get; set; }
		private void AddPromptButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(PromptNameComboBox, "Prompting category", "Select a prompting category that describes the way you want the AI to respond.");
			AppHelper.AddHelp(PromptOptionComboBox, "Prompting style", "Choose a specific style within the selected category to guide the AI's output.");
			AppHelper.AddHelp(AddPromptButton, "Add prompt", "Add a prompt to the user's message.");
		}
	}
}
