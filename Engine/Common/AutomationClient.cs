namespace JocysCom.VS.AiCompanion.Engine
{
	public class AutomationClient
	{
		public TemplateItem Item { get; set; }

		/// <summary>
		/// Get path to canvas AutomationElement
		/// </summary>
		public string GetCanvasEditorElementPath() => Item.CanvasEditorElementPath;

	}
}
