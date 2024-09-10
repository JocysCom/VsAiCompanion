using JocysCom.VS.AiCompanion.Plugins.Core;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginCategory
	{
		public PluginCategory() { }

		public PluginCategory(string name)
		{
			Name = name;
			var iconName = Resources.Icons.Icons_Default.Icon_radar;
			if (name.ToLower().Contains(nameof(Web).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_internet;
			if (name.ToLower().Contains(nameof(VisualStudio).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_Visual_Studio;
			if (name.ToLower().Contains(nameof(Database).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_database;
			if (name.ToLower().Contains(nameof(Lists).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_todo_list;
			if (name.ToLower().Contains(nameof(Search).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_magnifying_glass;
			if (name.ToLower().Contains(nameof(Automation).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_elements_tree;
			if (name.ToLower().Contains(nameof(Multimedia).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_movie_comment;
			if (name.ToLower().Contains(nameof(Mail).ToLower()))
				iconName = Resources.Icons.Icons_Default.Icon_mail;
			Icon = Engine.Resources.Icons.Icons_Default.Current[iconName];
		}
		public string Name { get; set; }
		public object Icon { get; }

	}
}
