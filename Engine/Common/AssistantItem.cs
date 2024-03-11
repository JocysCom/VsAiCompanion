using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AssistantItem : AiFileListItem
	{
		public AssistantItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		[DefaultValue("")]
		public string SystemMessage { get => _SystemMessage; set => SetProperty(ref _SystemMessage, value); }
		string _SystemMessage;

		#region App Settings Selections

		#endregion

	}
}
