using OpenAI;
using System.Net.Http;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public interface IClient
	{
		HttpClient GetClient();
		Task<Usage> GetUsageAsync();
	}
}
