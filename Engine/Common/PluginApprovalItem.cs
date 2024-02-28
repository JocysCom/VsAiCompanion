namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginApprovalItem : NotifyPropertyChanged
	{
		public PluginItem Plugin { get => _Plugin; set => SetProperty(ref _Plugin, value); }
		PluginItem _Plugin;

		public object[] Args { get => _Args; set => SetProperty(ref _Args, value); }
		object[] _Args;

		public bool? IsApproved { get => _IsApproved; set => SetProperty(ref _IsApproved, value); }
		bool? _IsApproved;
	}
}
