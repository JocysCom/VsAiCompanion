using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiMultimediaClient
	{

		public TemplateItem Item { get; set; }

		public async Task<OperationResult<string>> VideoToText(string instructions, string[] pathsOrUrls)
		{
			if (!Item.UseVideoToText)
				return new OperationResult<string>(new Exception($"Access denied. User must enable '{nameof(VideoToText)}' in Multimedia."));
			/// Try to get reserved template to generate title.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == Item.TemplateVideoToText);
			if (rItem == null)
				return new OperationResult<string>(new Exception($"Can't find '{Item.TemplateVideoToText}'"));
			if (string.IsNullOrWhiteSpace(instructions))
				return new OperationResult<string>(new Exception($"Instructions can't be empty!"));

			var messages = new List<chat_completion_message>();
			// Crate a copy in order not to add to existing list.
			try
			{
				// Add instructions to generate title to existing messages.
				if (!string.IsNullOrWhiteSpace(rItem.TextInstructions))
					messages.Add(new chat_completion_message(message_role.system, rItem.TextInstructions));
				var message = new chat_completion_message(message_role.user, instructions);
				var content = new List<content_item>();
				content.Add(new content_item { type = cotent_item_type.text, text = instructions });
				foreach (var file in pathsOrUrls)
				{
					var isUrl = Uri.TryCreate(file, UriKind.Absolute, out Uri uri) && uri.Scheme != Uri.UriSchemeFile;
					var url = file;
					if (!isUrl)
					{
						// Ensure the file exists before attempting to read it
						if (!File.Exists(file))
							return new OperationResult<string>(new FileNotFoundException("File not found", file));
						// Read the file's bytes and convert to base64 string
						var imageBytes = File.ReadAllBytes(file);
						url = ClassLibrary.Files.Mime.GetResourceDataUri(file, imageBytes);
					}
					// Create the object to be serialized
					var imageContent = new content_item
					{
						type = cotent_item_type.image_url,
						image_url = new image_url
						{
							url = url,
							detail = image_url_detail.auto,
						}
					};
					content.Add(imageContent);
				}
				message.content = content.ToArray();
				messages.Add(message);
				var client = new Companions.ChatGPT.Client(rItem.AiService);
				var maxInputTokens = Client.GetMaxInputTokens(rItem);
				// Send body and context data. Make sure it runs on NON-UI thread.
				var response = await Task.Run(async () => await client.QueryAI(
					rItem.AiModel,
					messages,
					rItem.Creativity,
					rItem,
					maxInputTokens,
					null
				)).ConfigureAwait(true);
				var body = response.FirstOrDefault()?.Body;
				return new OperationResult<string>(body);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

	}
}
