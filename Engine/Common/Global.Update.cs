using JocysCom.ClassLibrary.Controls.UpdateControl;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static partial class Global
	{
		public static UpdateChecker AiCompUpdateChecker;
		public static UpdateTimeChecker AiCompUpdateTimeChecker;
		public static UpdateTimeChecker PanDocUpdateTimeChecker;

		public static void InitUpdateTimeChecker()
		{
			UpdateChecker AiCompUpdateChecker = new UpdateChecker();



			// AI App update time checker.
			AiCompUpdateTimeChecker = new UpdateTimeChecker();
			AiCompUpdateTimeChecker.Settings = AppSettings.UpdateTimeSettings;
			AiCompUpdateTimeChecker.UpdateRequired += AiCompUpdateTimeChecker_UpdateRequired;
			AiCompUpdateTimeChecker.Start();
			// Pandoc update time checker.
			PanDocUpdateTimeChecker = new UpdateTimeChecker();
			PanDocUpdateTimeChecker.Settings = AppSettings.PandocUpdateTimeSettings;
			PanDocUpdateTimeChecker.UpdateRequired += PanDocUpdateTimeChecker_UpdateRequired;
			PanDocUpdateTimeChecker.Start();
		}

		public static void UnInitUpdateTimeChecker()
		{
			AiCompUpdateTimeChecker.UpdateRequired -= AiCompUpdateTimeChecker_UpdateRequired;
			AiCompUpdateTimeChecker.Stop();
			PanDocUpdateTimeChecker.UpdateRequired -= PanDocUpdateTimeChecker_UpdateRequired;
			PanDocUpdateTimeChecker.Stop();
		}

		private static void AiCompUpdateTimeChecker_UpdateRequired(object sender, System.EventArgs e)
		{

		}

		private static void PanDocUpdateTimeChecker_UpdateRequired(object sender, System.EventArgs e)
		{

		}

	}
}
