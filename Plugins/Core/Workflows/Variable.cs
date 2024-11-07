using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Workflows
{
	/// <summary>
	/// Represents a variable in the workflow plan.
	/// Variables are used to store data that can be passed between operations.
	/// </summary>
	public class Variable
	{
		/// <summary>
		/// Gets or sets the name of the variable.
		/// This is used to reference the variable within the workflow.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the value of the variable.
		/// Can be any object type.
		/// </summary>
		public JsonElement Value { get; set; }

	}
}
