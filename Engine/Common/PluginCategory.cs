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
			Icon = Engine.Resources.Icons.Icons_Default.Current[iconName];
		}

		public string Id { get; set; }
		public string Name { get; set; }
		public object Icon { get; }

	}
}
