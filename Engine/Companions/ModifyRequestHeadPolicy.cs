using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public class ModifyRequestHeadPolicy : PipelinePolicy
	{
		private readonly IDictionary<string, string> headers;

		public ModifyRequestHeadPolicy(IDictionary<string, string> headers)
		{
			this.headers = headers;
		}
		public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
		{
			// Add the headers to the request
			foreach (var header in headers)
				message.Request.Headers.Set(header.Key, header.Value);
			// Continue processing the next policy in the pipeline
			ProcessNext(message, pipeline, currentIndex + 1);
		}

		public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
		{
			var ms = new MemoryStream();
			if (message.Request.Content != null)
			{
				message.Request.Content?.WriteTo(ms);
				ms.Position = 0;
				// Convert binary data to string
				var jsonString = Encoding.UTF8.GetString(ms.ToArray());
				//var stringContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
				//var o = Client.Deserialize<OpenAI.Chat.ChatCompletionOptions>(jsonString);
				//// Create BinaryContent from the stream
				//var binaryContent = System.ClientModel.BinaryContent.Create(ms);
				//message.Request.Content = binaryContent;
			}
			// Add the headers to the request
			//foreach (var header in _headers)
			//{
			//	message.Request.Headers.Set(header.Key, header.Value);
			//}
			// Continue processing the next policy in the pipeline asynchronously
			await ProcessNextAsync(message, pipeline, currentIndex + 1).ConfigureAwait(false);
		}
	}
}
