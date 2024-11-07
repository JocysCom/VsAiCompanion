using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Workflows
{
	/// <summary>
	/// Represents an operation (method call) in the workflow plan.
	/// Each operation specifies a method to invoke, its arguments, and where to store the output.
	/// </summary>
	public class Operation
	{
		/// <summary>
		/// Gets or sets the name of the method to invoke.
		/// The method must be available in the provided method collection.
		/// </summary>
		public string MethodName { get; set; }

		/// <summary>
		/// Gets or sets the list of variable names to use as arguments.
		/// The variables must be defined in the plan's variable list.
		/// </summary>
		public List<string> Arguments { get; set; }

		/// <summary>
		/// Gets or sets the name of the variable to store the result.
		/// The result can then be used as an argument in subsequent operations.
		/// </summary>
		public string Output { get; set; }
	}
}
