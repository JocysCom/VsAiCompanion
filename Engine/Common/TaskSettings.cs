using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class TaskSettings: INotifyPropertyChanged
	{
		public double GridSplitterPosition { get; set; }

		[DefaultValue(true)]
		public bool IsListPanelVisible { get => _IsListPanelVisible; set => SetProperty(ref _IsListPanelVisible, value); }
		private bool _IsListPanelVisible = true;

		[DefaultValue(true)]
		public bool IsBarPanelVisible { get => _IsBarPanelVisible; set => SetProperty(ref _IsBarPanelVisible, value); }
		private bool _IsBarPanelVisible = true;

		//[DefaultValue([])]
		public List<string> ListSelection { get => _ListSelection; set => SetProperty(ref _ListSelection, value); }
		private List<string> _ListSelection;

		/// <summary>Remember "Search the list" text.</summary>
		[DefaultValue("")]
		public string SearchText { get => _SearchText; set => SetProperty(ref _SearchText, value); }
		private string _SearchText;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion


	}
}
