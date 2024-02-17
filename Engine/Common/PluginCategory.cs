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
			var iconName = "Icon_radar";
			if (Name.ToLower().Contains("visual") && Name.ToLower().Contains("studio"))
				iconName = "Icon_Visual_Studio";
			Icon = Engine.Resources.Icons.Icons_Default.Current[iconName];
		}

		public string Id { get; set; }
		public string Name { get; set; }
		public object Icon { get; }

	}
}
