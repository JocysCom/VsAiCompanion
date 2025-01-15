using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JocysCom.ClassLibrary.Collections;
using JocysCom.VS.AiCompanion.DataClient.Common;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for EmbeddingsControl.xaml
	/// </summary>
	public partial class EmbeddingsControl : UserControl
	{
		public EmbeddingsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Embeddings dropdown.
			Global.Embeddings.Items.ListChanged += Embeddings_Items_ListChanged;
			UpdateEmbeddingNames();

		}

		#region Embeddings

		public ObservableCollection<string> EmbeddingNames { get; set; } = new ObservableCollection<string>();

		public void UpdateEmbeddingNames()
		{
			var names = Global.Embeddings.Items.Select(x => x.Name).ToList();
			if (!names.Contains(""))
				names.Insert(0, "");
			CollectionsHelper.Synchronize(names, EmbeddingNames);
			OnPropertyChanged(nameof(EmbeddingNames));
		}

		public ObservableCollection<CheckBoxViewModel> EmbeddingGroupFlags
		{
			get
			{
				if (_EmbeddingGroupFlags == null)
					_EmbeddingGroupFlags = EnumComboBox.GetItemSource<EmbeddingGroupFlag>();
				EmbeddingHelper.UpdateGroupFlagsFromDatabase(Item?.UseEmbeddings == true ? Item?.EmbeddingName : null, _EmbeddingGroupFlags);
				return _EmbeddingGroupFlags;
			}
			set => _EmbeddingGroupFlags = value;
		}
		ObservableCollection<CheckBoxViewModel> _EmbeddingGroupFlags;

		public Dictionary<EmbeddingGroupFlag, string> FilePartGroups
			=> ClassLibrary.Runtime.Attributes.GetDictionary(
				(EmbeddingGroupFlag[])Enum.GetValues(typeof(EmbeddingGroupFlag)));

		public void EmbeddingGroupFlags_OnPropertyChanged()
		{
			OnPropertyChanged(nameof(EmbeddingGroupFlags));
		}

		private void Embeddings_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			var update = false;
			if (e.ListChangedType == ListChangedType.ItemChanged && e.PropertyDescriptor?.Name == nameof(TemplateItem.EmbeddingName))
				update = true;
			if (e.ListChangedType == ListChangedType.ItemAdded ||
				e.ListChangedType == ListChangedType.ItemDeleted)
				update = true;
			if (update)
				_ = Helper.Debounce(UpdateEmbeddingNames);
		}

		#endregion

		#region ■ Properties

		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
		}
		TemplateItem _Item;

		public async Task BindData(TemplateItem item)
		{
			await Task.Delay(0);
			if (Equals(item, _Item))
				return;
			if (_Item != null)
			{
				_Item.PropertyChanged -= _item_PropertyChanged;
			}
			_Item = item;
			if (_Item != null)
			{
				_Item.PropertyChanged += _item_PropertyChanged;
			}
			_ = Helper.Debounce(EmbeddingGroupFlags_OnPropertyChanged);
			OnPropertyChanged(nameof(Item));
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(TemplateItem.EmbeddingGroupName):
					_ = Helper.Debounce(EmbeddingGroupFlags_OnPropertyChanged);
					break;
				default:
					break;
			}
		}

		#endregion

		/// <summary>
		/// Handles the Loaded event of the user control.
		/// Initializes help content and UI presets when the control is loaded.
		/// </summary>
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		/// <summary>
		/// Handles the Unloaded event of the user control.
		/// Add any necessary cleanup logic here.
		/// </summary>
		private void This_Unloaded(object sender, RoutedEventArgs e)
		{
			// Cleanup logic can be added here if necessary.
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
