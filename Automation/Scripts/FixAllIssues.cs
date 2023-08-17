namespace JocysCom.VS.AiCompanion.Automation.Scripts
{
	public class FixAllIssues : IVsAutomate
	{
		public void ProcessEvent(VsAutomateEventInfo eventInfo)
		{
			switch (eventInfo.Stage)
			{
				case VsAutomateStage.None:
					break;
				case VsAutomateStage.BeforeSend:
					break;
				case VsAutomateStage.AfterSend:
					break;
				default:
					break;
			}
		}
	}
}
