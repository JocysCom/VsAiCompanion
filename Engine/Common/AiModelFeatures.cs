using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	[Flags]
	public enum AiModelFeatures : int
	{
		[Description("None")] None = 0,
		[Description("Streaming")] Streaming = 1 << 0,
		[Description("System Messages")] SystemMessages = 1 << 1,
		[Description("Function Calling")] FunctionCalling = 1 << 2,
		//[Description("Fine-Tuning")] FineTuning = 1 << 3,
		//[Description("Embeddings")] Embeddings = 1 << 4,
		//[Description("Moderation")] Moderation = 1 << 5,
		//[Description("Image Generation")] ImageGeneration = 1 << 6,
		//[Description("Image Recognition")] ImageRecognition = 1 << 7,
		//[Description("Audio Input")] AudioInput = 1 << 8,
		//[Description("Audio Output")] AudioOutput = 1 << 9,
		[Description("Chat Support")] ChatSupport = 1 << 10,
		//[Description("Code Generation")] CodeGeneration = 1 << 11,
		//[Description("Long Context Support")] LongContext = 1 << 12,
		//[Description("Multilingual Support")] Multilingual = 1 << 13,
		//[Description("Plugin Support")] PluginSupport = 1 << 14,
		//[Description("Sentiment Analysis")] SentimentAnalysis = 1 << 15,
		//[Description("Batch Processing")] BatchProcessing = 1 << 16,
		//[Description("Reinforcement Learning")] ReinforcementLearning = 1 << 17,
	}
}
