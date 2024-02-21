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
			if (Name.ToLower().Contains("visual") && Name.ToLower().Contains("studio"))
				iconName = Resources.Icons.Icons_Default.Icon_Visual_Studio;
			if (Name.ToLower().Contains("database"))
				iconName = Resources.Icons.Icons_Default.Icon_database;
			if (Name.ToLower().Contains("lists"))
				iconName = Resources.Icons.Icons_Default.Icon_todo_list;
			if (Name.ToLower().Contains("search"))
				iconName = Resources.Icons.Icons_Default.Icon_magnifying_glass;
			Icon = Engine.Resources.Icons.Icons_Default.Current[iconName];
		}

		public string Id { get; set; }
		public string Name { get; set; }
		public object Icon { get; }

	}
}
