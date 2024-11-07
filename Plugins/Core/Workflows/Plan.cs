using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Workflows
{
	/// <summary>
	/// Represents the workflow plan.
	/// The plan consists of a list of variables and a sequence of operations to execute.
	/// </summary>
	public class Plan
	{
		/// <summary>
		/// Gets or sets the list of variables defined in the plan.
		/// Variables provide data inputs and store outputs of operations.
		/// </summary>
		public List<Variable> Variables { get; set; }

		/// <summary>
		/// Gets or sets the list of operations (method calls) to execute.
		/// Operations are executed in the order they appear in this list.
		/// </summary>
		public List<Operation> Workflow { get; set; }
	}
}
