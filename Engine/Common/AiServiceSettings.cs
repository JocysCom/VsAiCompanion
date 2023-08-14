using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiServiceSettings : INotifyPropertyChanged
	{
		[DefaultValue(0.2)]
		public double GridSplitterPosition
		{
			get => _GridSplitterPosition == 0 ? 0.2 : _GridSplitterPosition;
			set => _GridSplitterPosition = value;
		}
		private double _GridSplitterPosition;

		public List<string> ListSelection { get => _ListSelection; set => SetProperty(ref _ListSelection, value); }
		private List<string> _ListSelection;

		/// <summary>
		/// Alternative selection is list selection is not available.
		/// </summary>
		public int ListSelectedIndex { get => _ListSelectedIndex; set => SetProperty(ref _ListSelectedIndex, value); }
		private int _ListSelectedIndex;

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
