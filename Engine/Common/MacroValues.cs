using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class MacroValues
	{
		/// <summary>Selection text inside active document.</summary>
		public DocItem Selection { get; set; } = new DocItem();

		/// <summary>Content of active document.</summary>
		public DocItem Document { get; set; } = new DocItem();

		/// <summary>Current date and time.</summary>
		public DateTime Date { get; set; } = DateTime.Now;
	}
}
