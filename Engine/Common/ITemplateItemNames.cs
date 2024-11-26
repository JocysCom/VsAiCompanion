namespace JocysCom.VS.AiCompanion.Engine
{
	public interface ITemplateItemNames
	{
		bool UseTextToVideo { get; set; }
		bool UseVideoToText { get; set; }
		bool UseCreateImage { get; set; }
		bool UseModifyImage { get; set; }

		string TemplateTextToVideo { get; set; }
		string TemplateVideoToText { get; set; }
		string TemplateCreateImage { get; set; }
		string TemplateModifyImage { get; set; }
	}
}
