using JocysCom.ClassLibrary.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PromptsControl.xaml
	/// </summary>
	public partial class PromptsControl : UserControl
	{
		public PromptsControl()
		{
			var names = Global.PromptItems.Items.Select(x => x.Name).ToList();
			PromptNames = new BindingList<string>(names);
			InitializeComponent();
		}

		TemplateItem _item;
		object bindLock = new object();

		public void BindData(TemplateItem item = null)
		{
			lock (bindLock)
			{
				if (Equals(item, _item))
					return;
				PromptOptionComboBox.SelectionChanged -= PromptOptionComboBox_SelectionChanged;
				_item = item;
				PromptOptionComboBox.SelectionChanged += PromptOptionComboBox_SelectionChanged;
			}
		}

		private void PromptOptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var selectedItem = e.AddedItems.Cast<string>().FirstOrDefault();
			var options = Global.PromptItems.Items.FirstOrDefault(x => x.Name == selectedItem)?.Options.ToList();
			CollectionsHelper.Synchronize(options, PromptOptions);
		}

		public BindingList<string> PromptNames { get; set; }

		public BindingList<string> PromptOptions { get; set; }

	}
}
