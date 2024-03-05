using JocysCom.ClassLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// List Info.
	/// </summary>
	public class ListInfo : ISettingsItemFile
	{
		/// <summary>List name.</summary>
		public string Name { get; set; }
		/// <summary>List description.</summary>
		public string Description { get; set; }
		/// <summary>Dictionary items</summary>
		public List<ListItem> Items { get; set; }

		#region ■ ISettingsItemFile

		[XmlIgnore]
		string ISettingsItemFile.BaseName { get => Name; set => Name = value; }

		[XmlIgnore]
		DateTime ISettingsItemFile.WriteTime { get; set; }

		#endregion
	}
}
