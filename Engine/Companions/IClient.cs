using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public interface IClient
	{
		HttpClient GetClient();
		Task<usage_response> GetUsageAsync();
	}
}
