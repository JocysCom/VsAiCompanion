using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using OpenAI.Images;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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


		#region Generate and Modify Images

		#region Helper Methods

		private (int imageWidth, int imageHeight) GetImageDimensions(Plugins.Core.image_size imageSize)
		{
			int imageWidth = 1024;
			int imageHeight = 1024;
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
			return (imageWidth, imageHeight);
		}

		private string SaveImageAndAddAttachment(Plugins.Core.VsFunctions.ImageInfo imageInfo, byte[] imageBytes)
		{
			var now = DateTime.Now;
			var pngName = $"image_{now:yyyyMMdd_HHmmss}_{imageInfo.Width}x{imageInfo.Height}.png";
			var jsonName = $"image_{now:yyyyMMdd_HHmmss}_{imageInfo.Width}x{imageInfo.Height}.json";
			// Gel locations and update image info.
			var folderPath = Global.GetPath(Item);
			if (!Directory.Exists(folderPath))
				Directory.CreateDirectory(folderPath);
			var pngFullPath = Path.Combine(folderPath, pngName);
			var jsonFullPath = Path.Combine(folderPath, jsonName);
			imageInfo.Name = pngName;
			imageInfo.FullName = pngFullPath;
			// Write image as PNG.
			File.WriteAllBytes(pngFullPath, imageBytes);
			// Write image info as JSON.
			var jsonContents = Client.Serialize(imageInfo);
			File.WriteAllText(jsonFullPath, jsonContents);
			// Get last messages from the chat list. It will be an assistant message.
			var message = Item.Messages.Last();
			message.Attachments.Add(new MessageAttachments
			{
				Title = pngName,
				Data = jsonContents,
				SendType = AttachmentSendType.None,
				Type = Plugins.Core.VsFunctions.ContextType.Image,
			});
			// Set date which will trigger update of the message on the chat web page.
			message.Updated = DateTime.Now;
			return pngFullPath;
		}

		private void AddTaskToUI(Guid id, CancellationTokenSource cancellationTokenSource)
		{
			ControlsHelper.AppInvoke(() =>
			{
				// If you have a UI component that tracks tasks, add this task to it
				Global.MainControl.InfoPanel.AddTask(id);
				Item.CancellationTokenSources.Add(cancellationTokenSource);
			});
		}

		private void RemoveTaskFromUI(Guid id, CancellationTokenSource cancellationTokenSource)
		{
			ControlsHelper.AppInvoke(() =>
			{
				Item.CancellationTokenSources.Remove(cancellationTokenSource);
				// Remove the task from the UI component
				Global.MainControl.InfoPanel.RemoveTask(id);
			});
		}

		private (Guid id, CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken) CreateOperationCancellationToken(int timeoutInSeconds)
		{
			var id = Guid.NewGuid();
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));
			var cancellationToken = cancellationTokenSource.Token;
			return (id, cancellationTokenSource, cancellationToken);
		}

		private async Task<ImageClient> GetImageClientAsync(TemplateItem rItem, CancellationToken cancellationToken)
		{
			var client = new Client(rItem.AiService);
			var aiClient = await client.GetAiClient(false, cancellationToken);
			return aiClient.GetImageClient(rItem.AiModel);
		}

		#endregion

		#region Generate and Modify Images

		public async Task<OperationResult<string>> GenerateImageAsync(
			string prompt,
			Plugins.Core.image_size imageSize = Plugins.Core.image_size.size_1024x1024,
			Plugins.Core.image_style imageStyle = Plugins.Core.image_style.vivid,
			Plugins.Core.image_quality imageQuality = Plugins.Core.image_quality.standard
		)
		{
			// Try to get reserved template item.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == Item.TemplateCreateImage);
			if (rItem == null)
				return new OperationResult<string>(new Exception($"Can't find '{Item.TemplateCreateImage}'"));
			if (string.IsNullOrWhiteSpace(prompt))
				return new OperationResult<string>(new Exception($"Prompt can't be empty!"));

			var (id, cancellationTokenSource, cancellationToken) = CreateOperationCancellationToken(rItem.AiService.ResponseTimeout);

			try
			{
				AddTaskToUI(id, cancellationTokenSource);

				var imageClient = await GetImageClientAsync(rItem, cancellationToken);

				var (imageWidth, imageHeight) = GetImageDimensions(imageSize);

				// Create image generation options
				var imageGenerationOptions = new ImageGenerationOptions()
				{
					Size = new GeneratedImageSize(imageWidth, imageHeight),
					Quality = new GeneratedImageQuality(imageQuality.ToString()),
					ResponseFormat = GeneratedImageFormat.Bytes,
					Style = new GeneratedImageStyle(imageStyle.ToString()),
				};
				// Call the image generation API
				var response = await imageClient.GenerateImageAsync(prompt, imageGenerationOptions, cancellationToken);
				var bytes = response?.Value?.ImageBytes;
				// Check if images are generated
				if (bytes != null)
				{
					var imageInfo = new Plugins.Core.VsFunctions.ImageInfo()
					{
						Prompt = prompt,
						Width = imageWidth,
						Height = imageHeight,
					};
					// Save the image and update messages
					var imageBytes = bytes.ToArray();
					var pngPath = SaveImageAndAddAttachment(imageInfo, imageBytes);
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
				RemoveTaskFromUI(id, cancellationTokenSource);
			}
		}

		private string GetMaskFileName(string originalImagePath)
		{
			var path = System.IO.Path.GetDirectoryName(originalImagePath);
			var baseName = System.IO.Path.GetFileNameWithoutExtension(originalImagePath);
			var ext = System.IO.Path.GetExtension(originalImagePath);
			var maskFullName = System.IO.Path.Combine(path, $"{baseName}.mask{ext}");
			return maskFullName;
		}

		private MemoryStream GetDefaultMaskStream(string originalImagePath)
		{
			var maskStream = new MemoryStream();
			// Create a default mask with the same dimensions as the original image
			using (var originalImage = new Bitmap(originalImagePath))
			{
				int width = originalImage.Width;
				int height = originalImage.Height;
				// Create a new bitmap for the mask with the same dimensions
				var maskBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				// Set the entire mask to fully transparent (alpha=0)
				using (Graphics g = Graphics.FromImage(maskBitmap))
				{
					// Fill the mask with transparent color
					g.Clear(Color.Transparent);
				}
				// Save the mask bitmap to a MemoryStream in PNG format
				maskBitmap.Save(maskStream, ImageFormat.Png);
				// Reset the position of the stream to the beginning
				maskStream.Position = 0;
				// Dispose of the mask bitmap
				maskBitmap.Dispose();
			}
			// Note: maskStream must remain open until after the API call
			return maskStream;
		}

		public async Task<OperationResult<string>> ModifyImageAsync(
			string originalImagePath,
			string prompt,
			string maskImagePath = null,
			Plugins.Core.image_size imageSize = Plugins.Core.image_size.size_1024x1024
		)
		{
			// Try to get reserved template item.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == Item.TemplateModifyImage);
			if (rItem == null)
				return new OperationResult<string>(new Exception($"Can't find '{Item.TemplateModifyImage}'"));
			if (string.IsNullOrWhiteSpace(prompt))
				return new OperationResult<string>(new Exception($"Prompt can't be empty!"));
			var (id, cancellationTokenSource, cancellationToken) = CreateOperationCancellationToken(rItem.AiService.ResponseTimeout);
			Stream maskStream = null;
			try
			{
				AddTaskToUI(id, cancellationTokenSource);
				var imageClient = await GetImageClientAsync(rItem, cancellationToken);
				var (imageWidth, imageHeight) = GetImageDimensions(imageSize);
				// Create image edit options
				var imageEditOptions = new ImageEditOptions()
				{
					Size = new GeneratedImageSize(imageWidth, imageHeight),
					ResponseFormat = GeneratedImageFormat.Bytes,
				};
				// Ensure the original image exists
				if (!File.Exists(originalImagePath))
					return new OperationResult<string>(new FileNotFoundException("Original image file not found", originalImagePath));
				var imageFilename = Path.GetFileName(originalImagePath);
				// Initialize mask stream and filename
				string maskFilename = null;
				if (!string.IsNullOrWhiteSpace(maskImagePath))
				{
					// Ensure the mask image exists
					if (!File.Exists(maskImagePath))
						return new OperationResult<string>(new FileNotFoundException("Mask image file not found", maskImagePath));
					// Open the mask image file as a stream
					maskStream = File.OpenRead(maskImagePath);
					maskFilename = Path.GetFileName(maskImagePath);
				}
				else
				{
					maskFilename = GetMaskFileName(originalImagePath);
					maskStream = GetDefaultMaskStream(originalImagePath);
				}
				ClientResult<GeneratedImage> response = null;
				// Open the original image file as a stream
				using (var imageStream = File.OpenRead(originalImagePath))
				{
					// Call the image editing API
					response = await imageClient.GenerateImageEditAsync(
						image: imageStream,
						imageFilename: imageFilename,
						prompt: prompt,
						mask: maskStream,
						maskFilename: maskFilename,
						options: imageEditOptions,
						cancellationToken: cancellationToken
					);
				}
				var bytes = response?.Value?.ImageBytes;
				// Check if images are generated
				if (bytes != null)
				{
					var imageInfo = new Plugins.Core.VsFunctions.ImageInfo()
					{
						Prompt = prompt,
						Width = imageWidth,
						Height = imageHeight,
					};
					// Save the image and update messages
					var imageBytes = bytes.ToArray();
					var pngPath = SaveImageAndAddAttachment(imageInfo, imageBytes);
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
				RemoveTaskFromUI(id, cancellationTokenSource);

				// Dispose of the mask stream if it was created
				if (maskStream != null)
					maskStream.Dispose();
			}
		}

		#endregion

		#endregion

	}
}
