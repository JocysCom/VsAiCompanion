using JocysCom.ClassLibrary.Configuration;
using System.ComponentModel;


namespace JocysCom.VS.AiCompanion.Engine
{
	public class UiPresetItem : SettingsListFileItem
	{
		public BindingList<VisibilityItem> Items { get; set; } = new BindingList<VisibilityItem>();
	}
}
