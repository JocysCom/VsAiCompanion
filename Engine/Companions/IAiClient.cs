using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public interface IAiClient
	{
		Task<List<MessageItem>> QueryAI(
			TemplateItem serviceItem,
			List<chat_completion_message> messagesToSend,
			string embeddingText);

		Task<OperationResult<Dictionary<int, float[]>>> GetEmbedding(
			string modelName,
			IEnumerable<string> input,
			CancellationToken cancellationToken = default);

		Task<model[]> GetModels();

		string LastError { get; set; }

		#region Fine Tuning - Models

		Task<fine_tune> CreateFineTuneJob(fine_tune_request r);
		Task<fine_tune> CancelFineTuningJobAsync(string id, CancellationToken cancellationToken = default);

		Task<List<fine_tuning_jobs_response>> GetFineTuningJobsAsync(fine_tuning_jobs_request request);

		Task<deleted_response> DeleteFineTuningJobAsync(string id, CancellationToken cancellationToken = default);

		Task<deleted_response> DeleteModelAsync(string id, CancellationToken cancellationToken = default);


		#endregion

		#region Fine Tuning - Files

		Task<List<files>> GetFilesAsync();

		Task<deleted_response> DeleteFileAsync(string id, CancellationToken cancellationToken = default);

		Task<file> UploadFileAsync(string filePath, string purpose, CancellationToken cancellationToken = default);

		#endregion
	}
}
