using JocysCom.ClassLibrary.Configuration;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// List Info.
	/// </summary>
	public class ListInfo : SettingsListFileItem
	{

		/// <summary>
		/// List Info
		/// </summary>
		public ListInfo()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>List description used for the user.</summary>
		[DefaultValue("")]
		public string Description { get => _Description; set => SetProperty(ref _Description, value); }
		string _Description;

		/// <summary>List Instructions for AI.</summary>
		[DefaultValue("")]
		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, value); }
		string _Instructions;

		/// <summary>If 'true' then AI can't modify the list.</summary>
		[DefaultValue(false)]
		public bool IsReadOnly { get => _IsReadOnly; set => SetProperty(ref _IsReadOnly, value); }
		bool _IsReadOnly;

		/// <summary>List items: Key, Value, Comment.</summary>
		[DefaultValue(null)]
		public BindingList<ListItem> Items { get => _Items; set => SetProperty(ref _Items, value); }
		BindingList<ListItem> _Items;

		/// <inheritdoc/>
		public override bool IsEmpty
		{
			get { return (_Items?.Count ?? 0) == 0; }
		}

	}
}
