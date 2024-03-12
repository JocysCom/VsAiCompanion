using JocysCom.VS.AiCompanion.Plugins.Core;
using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginCategory
	{
		public PluginCategory() { }

		public PluginCategory(Type methodInfoDeclaringType)
		{
			Id = methodInfoDeclaringType.FullName;
			Name = methodInfoDeclaringType.Name;
			var iconName = Resources.Icons.Icons_Default.Icon_radar;
			if (Name.ToLower().Contains(nameof(Web).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_internet;
			if (Name.ToLower().Contains(nameof(VisualStudio).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_Visual_Studio;
			if (Name.ToLower().Contains(nameof(Database).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_database;
			if (Name.ToLower().Contains(nameof(Lists).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_todo_list;
			if (Name.ToLower().Contains(nameof(Search).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_magnifying_glass;
			if (Name.ToLower().Contains(nameof(Automation).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_elements_tree;
			if (Name.ToLower().Contains(nameof(TTS).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_chat;
			Icon = Engine.Resources.Icons.Icons_Default.Current[iconName];
		}

		public string Id { get; set; }
		public string Name { get; set; }
		public object Icon { get; }

	}
}
