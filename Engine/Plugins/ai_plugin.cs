namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class ai_plugin
	{
		public string schema_version { get; set; }
		public string name_for_human { get; set; }
		public string name_for_model { get; set; }
		public string description_for_human { get; set; }
		public string description_for_model { get; set; }
		public ai_plugin_auth auth { get; set; }
		public ai_plugin_api api { get; set; }
		public string logo_url { get; set; }
		public string contact_email { get; set; }
		public string legal_info_url { get; set; }
	}
}
