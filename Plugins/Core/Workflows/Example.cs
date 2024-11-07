using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Workflows
{
	/// <summary>
	/// Demonstrates how to use the WorkflowExecutor to execute a workflow plan.
	/// </summary>
	internal class Example
	{
		/// <summary>
		/// Sample function that takes an integer argument and returns a formatted string.
		/// </summary>
		/// <param name="arg1">An integer argument.</param>
		/// <returns>A formatted string containing the argument value.</returns>
		public static string FunctionA(int arg1)
		{
			return $"Result of FunctionA with arg1={arg1}";
		}

		/// <summary>
		/// Sample function that takes a string input and returns a boolean indicating if the string is not null or empty.
		/// </summary>
		/// <param name="input">A string input.</param>
		/// <returns>True if the input is not null or empty; otherwise, false.</returns>
		public static bool FunctionB(string input)
		{
			return !string.IsNullOrEmpty(input);
		}

		/// <summary>
		/// Tests the workflow execution by creating a plan and executing it using WorkflowExecutor.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token for asynchronous operations (unused in this example).</param>
		public async Task Test(CancellationToken cancellationToken = default)
		{
			// Prepare the methods array by retrieving MethodInfo objects for the sample functions
			var methods = new MethodInfo[]
			{
				typeof(Example).GetMethod(nameof(Example.FunctionA)),
				typeof(Example).GetMethod(nameof(Example.FunctionB))
			};

			// Define the JSON representation of the workflow plan
			string jsonPlan = @"
            {
                ""Variables"": [
                    {""Name"": ""arg1"", ""Value"": 42}
                ],
                ""Workflow"": [
                    {
                        ""MethodName"": ""FunctionA"",
                        ""Arguments"": [""arg1""],
                        ""Output"": ""resultA""
                    },
                    {
                        ""MethodName"": ""FunctionB"",
                        ""Arguments"": [""resultA""],
                        ""Output"": ""resultB""
                    }
                ]
            }";

			// Deserialize the JSON plan into a Plan object
			var plan = JsonSerializer.Deserialize<Plan>(jsonPlan);

			// Create an instance of WorkflowExecutor and execute the plan
			var executor = new WorkflowExecutor();
			await executor.ExecutePlan(plan, methods, cancellationToken);

			// Since this is a synchronous execution, the Task can complete immediately
			await Task.CompletedTask;
		}
	}
}
