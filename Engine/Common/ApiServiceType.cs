using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Enumeration for various AI API service types, each identified by the company and product name.
	/// </summary>
	public enum ApiServiceType
	{
		/// <summary>
		/// No API service selected.
		/// </summary>
		[Description("")]
		None,

		/// <summary>
		/// OpenAI's GPT-based API services (e.g., GPT-3, GPT-4, ChatGPT API).
		/// </summary>
		[Description("OpenAI")]
		OpenAI,

		/// <summary>
		/// Microsoft Azure Cognitive Services.
		/// </summary>
		[Description("Microsoft Azure")]
		Azure,

		/// <summary>
		/// Custom AI Plugin service.
		/// </summary>
		[Description("Custom AI Plugin")]
		AiPlugin,


		/*

		/// <summary>
		/// Google's Vertex AI service.
		/// </summary>
		[Description("Google Gemini (OpenAI)")]
		GoogleGeminiOpenAI,

		/// <summary>
		/// Anthropic's Claude AI service.
		/// </summary>
		[Description("Anthropic Claude")]
		AnthropicClaude,

		/// <summary>
		/// Google's Vertex AI service.
		/// </summary>
		[Description("Google Vertex AI")]
		GoogleVertexAI,

		/// <summary>
		/// Meta's LLaMA AI model and services.
		/// </summary>
		[Description("Meta LLaMA")]
		MetaLLaMA,

		/// <summary>
		/// IBM's Watson AI services.
		/// </summary>
		[Description("IBM Watson")]
		IBMWatson,

		/// <summary>
		/// Amazon Web Services' AI services (e.g., SageMaker, Comprehend).
		/// </summary>
		[Description("Amazon AWS AI Services")]
		AmazonAwsAiServices,

		/// <summary>
		/// Hugging Face AI platform and models.
		/// </summary>
		[Description("Hugging Face")]
		HuggingFace,

		/// <summary>
		/// Stability AI's services (e.g., Stable Diffusion).
		/// </summary>
		[Description("Stability AI")]
		StabilityAI,

		/// <summary>
		/// xAI's Grok AI service.
		/// </summary>
		[Description("xAI Grok")]
		XaiGrok,

		/// <summary>
		/// Cohere AI language models and services.
		/// </summary>
		[Description("Cohere AI")]
		CohereAI,

		/// <summary>
		/// AI21 Labs' Jurassic language models.
		/// </summary>
		[Description("AI21 Labs Jurassic")]
		AI21LabsJurassic

		*/

	}


}
