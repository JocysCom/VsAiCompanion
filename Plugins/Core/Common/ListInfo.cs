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

		/// <summary>List description.</summary>
		[DefaultValue("")]
		public string Description { get => _Description; set => SetProperty(ref _Description, value); }
		string _Description;

		/// <summary>Dictionary items</summary>
		public List<ListItem> Items { get => _Items; set => SetProperty(ref _Items, value); }
		List<ListItem> _Items;

	}
}
