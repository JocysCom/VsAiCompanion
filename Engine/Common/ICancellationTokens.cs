using System.ComponentModel;
using System.Threading;

namespace JocysCom.VS.AiCompanion.Engine
{
	public interface  ICancellationTokens
	{
		 bool IsBusy { get; set; }
		 BindingList<CancellationTokenSource> CancellationTokenSources { get; set; }
		 void StopClients();
	}
}
