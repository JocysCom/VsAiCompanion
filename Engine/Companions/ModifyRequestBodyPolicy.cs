using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	/// <summary>
	/// A pipeline policy to intercept, modify, and re-serialize JSON request bodies.
	/// </summary>
	public class ModifyRequestBodyPolicy : PipelinePolicy
	{

		private readonly IDictionary<string, string> properties;

		public ModifyRequestBodyPolicy(IDictionary<string, string> properties)
		{
			this.properties = properties;
		}

		public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
		{
			ModifyRequestBodyAsync(message, pipeline, currentIndex, asyncCall: false).GetAwaiter().GetResult();
		}

		public override async ValueTask ProcessAsync(
			PipelineMessage message,
			IReadOnlyList<PipelinePolicy> pipeline,
			int currentIndex)
		{
			await ModifyRequestBodyAsync(message, pipeline, currentIndex, asyncCall: true).ConfigureAwait(false);
		}

		private async Task ModifyRequestBodyAsync(
			PipelineMessage message,
			IReadOnlyList<PipelinePolicy> pipeline,
			int currentIndex,
			bool asyncCall)
		{
			var request = message.Request;

			// Only attempt to read/modify if there's a request body
			if (request.Content != null)
			{
				// 1) Read request content into memory
				using (var ms = new MemoryStream())
				{
					if (asyncCall)
						await request.Content.WriteToAsync(ms, default).ConfigureAwait(false);
					else
						request.Content.WriteTo(ms, default);

					// 2) Convert stream to string
					ms.Position = 0;
					var jsonString = Encoding.UTF8.GetString(ms.ToArray());

					// 3) Convert your request to a base_item so that unknown fields are preserved
					//    This is crucial so you do not lose other JSON fields the AI client sets.
					//    If all of your request bodies are the same type (e.g., ChatCompletionOptions),
					//    you could deserialize to that type. But “base_item” is a good fallback
					//    because it has [JsonExtensionData].
					var requestObject = JsonSerializer.Deserialize<base_item>(jsonString, ChatGPT.Client.GetJsonOptions())
										?? new base_item();

					// 4) Add your custom parameters
					foreach (var property in properties)
						requestObject.AddProperty(property.Key, property.Value);

					// 5) Re-serialize into new JSON string
					var newJsonString = JsonSerializer.Serialize(requestObject, ChatGPT.Client.GetJsonOptions());
					var newBodyMs = new MemoryStream(Encoding.UTF8.GetBytes(newJsonString));
					var newBodyContent = System.ClientModel.BinaryContent.Create(newBodyMs);

					// 6) Replace the request content (BinaryData) with the updated JSON
					request.Content = newBodyContent;
					request.Headers.Remove("Content-Length");
					request.Headers.Add("Content-Type", "application/json");
				}
			}

			// Let the pipeline continue
			if (asyncCall)
			{
				await ProcessNextAsync(message, pipeline, currentIndex + 1).ConfigureAwait(false);
			}
			else
			{
				ProcessNext(message, pipeline, currentIndex + 1);
			}
		}
	}
}
