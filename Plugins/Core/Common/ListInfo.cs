using JocysCom.ClassLibrary.Configuration;
using System.Collections.Generic;
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

		/// <summary>List description sent to AI.</summary>
		[DefaultValue("")]
		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, value); }
		string _Instructions;

		/// <summary>List description sent to AI.</summary>
		[DefaultValue(false)]
		public bool IsReadOnly { get => _IsReadOnly; set => SetProperty(ref _IsReadOnly, value); }
		bool _IsReadOnly;

		/// <summary>Dictionary items</summary>
		[DefaultValue(null)]
		public List<ListItem> Items { get => _Items; set => SetProperty(ref _Items, value); }
		List<ListItem> _Items;

	}
}
