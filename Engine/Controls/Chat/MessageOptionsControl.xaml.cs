using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	/// <summary>
	/// Interaction logic for MessageOptionsControl.xaml
	/// </summary>
	public partial class MessageOptionsControl : UserControl
	{
		public MessageOptionsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			MessageTypeBox.ItemsSource = Enum.GetValues(typeof(MessageType));
		}

		MessageItem _Item;
		public MessageItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				// Update from previous settings.
				if (_Item != null)
				{
					_Item.PropertyChanged -= _item_PropertyChanged;
				}
				_Item = value;
				if (_Item != null)
				{
					_Item.PropertyChanged += _item_PropertyChanged;
				}
			}
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(MessageItem.Type):

					break;
				default:
					break;
			}
		}

		private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			Item = DataContext as MessageItem;
		}
	}
}
