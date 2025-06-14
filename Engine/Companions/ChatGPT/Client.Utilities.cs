using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using OpenAI.Chat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Utility methods for content conversion, embeddings, and helper functions.
	/// </summary>
	public partial class Client
	{
		#region Embeddings

		/// <summary>
		/// Get embedding vectors.
		/// </summary>
		/// <param name="modelName">The model name to use for embeddings.</param>
		/// <param name="input">The input text to generate embeddings for.</param>
		/// <param name="cancellationToken">Cancellation token for the operation.</param>
		/// <returns>Operation result containing embedding vectors indexed by position.</returns>
		public async Task<OperationResult<Dictionary<int, float[]>>> GetEmbedding(
			string modelName,
			IEnumerable<string> input,
			CancellationToken cancellationToken = default
			)
		{
			var client = await GetAiClient(false);
			var clientToken = new CancellationTokenSource();
			clientToken.CancelAfter(TimeSpan.FromSeconds(Service.ResponseTimeout));
			var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(clientToken.Token, cancellationToken);
			var id = Guid.NewGuid();
			ControlsHelper.AppInvoke(() =>
			{
				//item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
			});
			Dictionary<int, float[]> results = null;
			try
			{
				var embeddingClient = client.GetEmbeddingClient(modelName);
				var response = await embeddingClient.GenerateEmbeddingsAsync(input, cancellationToken: linkedTokenSource.Token);
				if (response != null)
				{
					var inputTokens = response.Value.Usage.InputTokenCount;
					var totalTokens = response.Value.Usage.TotalTokenCount;
					results = response.Value
						.ToDictionary(x => x.Index, x => x.ToFloats().ToArray());
				}
			}
			catch (Exception ex)
			{
				return new OperationResult<Dictionary<int, float[]>>(ex);
			}
			finally
			{
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(id);
				});
			}
			return new OperationResult<Dictionary<int, float[]>>(results);
		}

		#endregion

		#region Content Conversion

		/// <summary>
		/// Converts a content item to a ChatMessageContentPart for API communication.
		/// </summary>
		/// <param name="o">The content item to convert.</param>
		/// <returns>ChatMessageContentPart or null if conversion is not possible.</returns>
		public ChatMessageContentPart ConvertToChatMessageContentItem(object o)
		{
			if (!(o is content_item item))
				return null;
			switch (item.type)
			{
				case cotent_item_type.text:
					return ChatMessageContentPart.CreateTextPart(item.text);
				case cotent_item_type.image_url:
					// The Microsoft Uri has a size limit of x0FFF0.
					// At the moment the ChatMessageImageUrl does not support attaching base64 images larger than that.
					var detail = (ChatImageDetailLevel)item.image_url.detail.ToString();
					ChatMessageContentPart ci = null;
					if (ClassLibrary.Files.Mime.TryParseDataUri(item.image_url.url, out string mimeType, out byte[] data))
					{
						var bytes = BinaryData.FromBytes(data);
						ci = ChatMessageContentPart.CreateImagePart(bytes, mimeType, detail);
					}
					else
					{
						var imageUri = new System.Uri(item.image_url.url);
						ci = ChatMessageContentPart.CreateImagePart(imageUri, detail);
					}
					return ci;
				default:
					return null;
			}
		}

		/// <summary>
		/// Converts an object to a NameValueCollection for URL query parameters.
		/// </summary>
		/// <param name="o">The object to convert.</param>
		/// <param name="escapeForUrl">Whether to URL-encode the values.</param>
		/// <returns>NameValueCollection with object properties as key-value pairs.</returns>
		public NameValueCollection ConvertToNameValueCollection(object o, bool escapeForUrl = false)
		{
			var collection = HttpUtility.ParseQueryString(string.Empty);
			var props = o.GetType().GetProperties();
			// Get all properties of the object
			foreach (var prop in props)
			{
				// Get property value
				var value = prop.GetValue(o);
				// If value is default for its type, skip serialization
				if (value == null || value.Equals(GetDefault(prop.PropertyType)))
					continue;
				// Convert property value to Json string
				var jsonValue = System.Text.Json.JsonSerializer.Serialize(value);
				// If escapeForUrl flag is set, URL encode the name and value
				var key = escapeForUrl ? Uri.EscapeDataString(prop.Name) : prop.Name;
				var val = escapeForUrl ? Uri.EscapeDataString(jsonValue) : jsonValue;
				// Add property name and value to the collection
				collection[key] = val;
			}
			return collection;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Cache for default values by type to improve performance.
		/// </summary>
		private static ConcurrentDictionary<Type, object> _defaultValuesCache = new ConcurrentDictionary<Type, object>();

		/// <summary>
		/// Gets the default value for the specified type.
		/// </summary>
		/// <param name="type">The type to get the default value for.</param>
		/// <returns>Default value for the type.</returns>
		private static object GetDefault(Type type)
		{
			return _defaultValuesCache.GetOrAdd(type, t => (t.IsValueType ? Activator.CreateInstance(t) : null));
		}

		#endregion
	}
}
