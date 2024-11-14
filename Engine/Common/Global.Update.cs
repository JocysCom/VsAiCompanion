using JocysCom.ClassLibrary.Controls.UpdateControl;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static partial class Global
	{
		public static UpdateTimeChecker MainCompUpdateTimeChecker;
		public static UpdateChecker MainCompUpdateChecker;

		public static UpdateTimeChecker PanDocUpdateTimeChecker;
		public static UpdateChecker PanDocUpdateChecker;

		public static void InitUpdateTimeChecker()
		{
			// AI App update time checker.
			MainCompUpdateTimeChecker = new UpdateTimeChecker();
			MainCompUpdateTimeChecker.Settings = AppSettings.UpdateTimeSettings;
			MainCompUpdateTimeChecker.UpdateRequired += AiCompUpdateTimeChecker_UpdateRequired;
			MainCompUpdateChecker = new UpdateChecker();
			MainCompUpdateChecker.Settings = AppSettings.UpdateSettings;
			MainCompUpdateTimeChecker.Start();
			// Pandoc update time checker.
			var pandocSettings = AppSettings.PandocUpdateSettings;
			UpdateMissingPandocDefaults(pandocSettings);
			PanDocUpdateTimeChecker = new UpdateTimeChecker();
			PanDocUpdateTimeChecker.Settings = AppSettings.PandocUpdateTimeSettings;
			PanDocUpdateTimeChecker.UpdateRequired += PanDocUpdateTimeChecker_UpdateRequired;
			PanDocUpdateChecker = new UpdateChecker();
			PanDocUpdateChecker.Settings = pandocSettings;
			PanDocUpdateTimeChecker.Start();
		}

		public static void UpdateMissingPandocDefaults(UpdateSettings us)
		{
			if (string.IsNullOrWhiteSpace(us.GitHubCompany))
				us.GitHubCompany = "jgm";
			if (string.IsNullOrWhiteSpace(us.GitHubProduct))
				us.GitHubProduct = "pandoc";
			if (string.IsNullOrWhiteSpace(us.GitHubAssetName))
				us.GitHubAssetName = "windows-x86_64.zip";
			if (string.IsNullOrWhiteSpace(us.FileNameInsideZip))
				us.FileNameInsideZip = "pandoc.exe";
			if (string.IsNullOrWhiteSpace(us.MinVersion))
				us.MinVersion = "3.1";
		}

		public static void UnInitUpdateTimeChecker()
		{
			MainCompUpdateTimeChecker.UpdateRequired -= AiCompUpdateTimeChecker_UpdateRequired;
			MainCompUpdateTimeChecker.Stop();
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
