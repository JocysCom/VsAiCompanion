using System;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public static class AiClientFactory
	{
		public static IAiClient GetAiClient(AiService aiService)
		{
			switch (aiService.ServiceType)
			{
				case ApiServiceType.OpenAI:
					return new Companions.ChatGPT.Client(aiService);
				//case ApiServiceType.Meta:
				//	return new Companions.Meta.Client(aiService);
				//case ApiServiceType.Google:
				//	return new Companions.Google.Client(aiService);
				//case ApiServiceType.Anthropic:
				//	return new Companions.Anthropic.Client(aiService);
				//case ApiServiceType.X:
				//	return new Companions.X.Client(aiService);
				default:
					throw new NotSupportedException($"AI Provider '{aiService.ServiceType}' not supported");
			}
		}
	}
}
