using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	public interface IAiServiceModel
	{
		Guid AiServiceId { get; set; }
		AiService AiService { get; }
		string AiModel { get; set; }
	}
}
