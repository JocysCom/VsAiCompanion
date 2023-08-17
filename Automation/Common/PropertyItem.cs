namespace JocysCom.VS.AiCompanion.Automation
{
	public class PropertyItem
	{

		public PropertyItem(string key = null, string value = null, string display = null)
		{
			Key = key;
			Value = value;
			Display = display;
		}

		public string Key {get; set;}
		public string Value { get; set; }
		public string Display { get; set; }
	}
}
