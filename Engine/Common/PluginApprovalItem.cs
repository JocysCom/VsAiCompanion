using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Threading;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginApprovalItem : NotifyPropertyChanged
	{
		public PluginItem Plugin { get => _Plugin; set => SetProperty(ref _Plugin, value); }
		PluginItem _Plugin;

		public bool? IsApproved { get => _IsApproved; set => SetProperty(ref _IsApproved, value); }
		bool? _IsApproved;

		public string SecondaryAiEvaluation { get => _SecondaryAiEvaluation; set => SetProperty(ref _SecondaryAiEvaluation, value); }
		string _SecondaryAiEvaluation;

		public chat_completion_function function;

		public SemaphoreSlim Semaphore = new SemaphoreSlim(0);
	}
}
