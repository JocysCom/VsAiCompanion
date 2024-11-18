using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using OpenAI.Images;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AiMultimediaClient
	{

		public TemplateItem Item { get; set; }

		public async Task<OperationResult<string>> VideoToText(string prompt, string[] pathsOrUrls)
		{
			if (!Item.UseVideoToText)
				return new OperationResult<string>(new Exception($"Access denied. User must enable '{nameof(VideoToText)}' in Multimedia."));
			// Try to get reserved template item.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == Item.TemplateVideoToText);
			if (rItem == null)
				return new OperationResult<string>(new Exception($"Can't find '{Item.TemplateVideoToText}'"));
			if (string.IsNullOrWhiteSpace(prompt))
				return new OperationResult<string>(new Exception($"Prompt can't be empty!"));

			var messages = new List<chat_completion_message>();
			// Crate a copy in order not to add to existing list.
			try
			{
				if (!string.IsNullOrWhiteSpace(rItem.TextInstructions))
					messages.Add(new chat_completion_message(message_role.system, rItem.TextInstructions));
				var message = new chat_completion_message(message_role.user, prompt);
				var content = new List<content_item>();
				content.Add(new content_item { type = cotent_item_type.text, text = prompt });
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
				var client = AiClientFactory.GetAiClient(rItem.AiService);
				// Send body and context data. Make sure it runs on NON-UI thread.
				var response = await Task.Run(async () => await client.QueryAI(
					rItem,
					messages,
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


		#region Generate Images

		public async Task<OperationResult<string>> GenerateImageAsync(
			string prompt,
			Plugins.Core.image_size imageSize = Plugins.Core.image_size.size_1024x1024,
			Plugins.Core.image_style imageStyle = Plugins.Core.image_style.vivid,
			Plugins.Core.image_quality imageQuality = Plugins.Core.image_quality.standard
		)
		{
			// Try to get reserved template item.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == Item.TemplateGenerateImage);
			if (rItem == null)
				return new OperationResult<string>(new Exception($"Can't find '{Item.TemplateGenerateImage}'"));
			if (string.IsNullOrWhiteSpace(prompt))
				return new OperationResult<string>(new Exception($"Prompt can't be empty!"));

			var id = Guid.NewGuid();
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(rItem.AiService.ResponseTimeout));
			var cancellationToken = cancellationTokenSource.Token;

			try
			{

				ControlsHelper.AppInvoke(() =>
				{
					// If you have a UI component that tracks tasks, add this task to it
					Global.MainControl.InfoPanel.AddTask(id);
					Item.CancellationTokenSources.Add(cancellationTokenSource);

				});
				var client = new Client(rItem.AiService);
				var aiClient = await client.GetAiClient(cancellationToken);

				var imageWidth = 1024;
				var imageHeight = 1024;
				if (imageSize == Plugins.Core.image_size.size_1792x1024)
				{
					imageWidth = 1792;
					imageHeight = 1024;
				}
				else if (imageSize == Plugins.Core.image_size.size_1024x1792)
				{
					imageWidth = 1024;
					imageHeight = 1792;
				}
				// Create image generation options
				var imageGenerationOptions = new ImageGenerationOptions()
				{
					Size = new GeneratedImageSize(imageWidth, imageHeight),
					Quality = new GeneratedImageQuality(imageQuality.ToString()),
					ResponseFormat = GeneratedImageFormat.Bytes,
					Style = new GeneratedImageStyle(imageStyle.ToString()),
				};
				// Call the image generation API
				var imageClient = aiClient.GetImageClient(rItem.AiModel);
				var response = await imageClient.GenerateImageAsync(prompt, imageGenerationOptions, cancellationToken);
				var bytes = response?.Value?.ImageBytes;
				// Check if images are generated
				if (bytes != null)
				{
					// Return the URI of the first generated image
					var imageBytes = bytes.ToArray();
					var fileName = $"{Guid.NewGuid()}.png";
					// Save image here.
					var folderPath = Global.GetPath(Item, "Images");
					var relativePath = System.IO.Path.Combine(Item.Name, "Images", fileName);
					if (!Directory.Exists(folderPath))
						Directory.CreateDirectory(folderPath);
					var pngPath = System.IO.Path.Combine(folderPath, fileName);
					System.IO.File.WriteAllBytes(pngPath, imageBytes);
					var message = Item.Messages.Last();
					message.Attachments.Add(new MessageAttachments
					{
						Title = "Generated Image",
						Data = relativePath,
						Location = pngPath,
						SendType = AttachmentSendType.None,
						Type = Plugins.Core.VsFunctions.ContextType.Image,
					});
					return new OperationResult<string>(pngPath);
				}
				else
				{
					return new OperationResult<string>(new Exception("No images were generated."));
				}
			}
			catch (Exception ex)
			{
				// Return any exceptions encountered
				return new OperationResult<string>(ex);
			}
			finally
			{
				ControlsHelper.AppInvoke(() =>
				{
					Item.CancellationTokenSources.Remove(cancellationTokenSource);
					// Remove the task from the UI component
					Global.MainControl.InfoPanel.RemoveTask(id);
				});
			}
		}

		#endregion

	}
}
