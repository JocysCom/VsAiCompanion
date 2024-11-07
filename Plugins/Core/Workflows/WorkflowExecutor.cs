using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Workflows
{
	/// <summary>
	/// Provides functionality to execute a workflow plan by invoking specified methods.
	/// The executor manages variables and handles method invocation using reflection.
	/// </summary>
	public class WorkflowExecutor
	{
		/// <summary>
		/// Stores the variables used in the workflow execution.
		/// The key is the variable name, and the value is the variable's value.
		/// </summary>
		private Dictionary<string, JsonElement> variables = new Dictionary<string, JsonElement>();

		/// <summary>
		/// Executes the workflow plan by invoking methods defined in the plan.
		/// </summary>
		/// <param name="plan">The workflow plan containing variables and operations to execute.</param>
		/// <param name="methods">An array of methods available for invocation.</param>
		/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
		public async Task ExecutePlan(Plan plan, MethodInfo[] methods, CancellationToken cancellationToken = default)
		{
			await Task.Delay(0);
			// Initialize variables from the plan's variable list
			if (plan.Variables != null)
			{
				foreach (var variable in plan.Variables)
					variables[variable.Name] = variable.Value;
			}

			// Execute each operation in the workflow sequence
			foreach (var operation in plan.Workflow)
			{
				if (cancellationToken.IsCancellationRequested)
					return;
				// Find the method to invoke by name
				MethodInfo methodInfo = methods.FirstOrDefault(m => m.Name == operation.MethodName);
				if (methodInfo == null)
				{
					Console.WriteLine($"Method '{operation.MethodName}' not found.");
					continue; // Skip to the next operation
				}

				// Get the method parameters
				var parameters = methodInfo.GetParameters();
				int expectedArgumentCount = parameters.Length;
				int providedArgumentCount = operation.Arguments?.Count ?? 0;

				// Check if the number of provided arguments matches the method's parameter count
				if (expectedArgumentCount != providedArgumentCount)
				{
					Console.WriteLine($"Argument count mismatch for method '{operation.MethodName}'. Expected {expectedArgumentCount}, but got {providedArgumentCount}.");
					continue; // Skip to the next operation
				}

				// Prepare the arguments for method invocation
				var args = new object[expectedArgumentCount];
				bool argumentPreparationFailed = false;

				for (int i = 0; i < expectedArgumentCount; i++)
				{
					var argName = operation.Arguments[i];
					if (!variables.TryGetValue(argName, out var argValue))
					{
						Console.WriteLine($"Variable '{argName}' not found for method '{operation.MethodName}'.");
						argumentPreparationFailed = true;
						break;
					}
					try
					{
						args[i] = Convert.ChangeType(argValue, parameters[i].ParameterType);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error converting argument '{argName}' to type '{parameters[i].ParameterType.Name}' for method '{operation.MethodName}': {ex.Message}");
						argumentPreparationFailed = true;
						break;
					}
				}

				if (argumentPreparationFailed)
				{
					continue; // Skip to the next operation
				}

				// Invoke the method using reflection
				try
				{
					var result = methodInfo.Invoke(null, args);

					// Store the result in variables if an output variable name is specified
					if (!string.IsNullOrEmpty(operation.Output))
					{
						var jsonString = JsonSerializer.Serialize(result);
						using (JsonDocument document = JsonDocument.Parse(jsonString))
						{
							var jsonElValue = document.RootElement.Clone();
							variables[operation.Output] = jsonElValue;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error invoking method '{operation.MethodName}': {ex.Message}");
				}
			}

			// Output the final variables for inspection
			Console.WriteLine("Final Variables:");
			foreach (var variable in variables)
			{
				Console.WriteLine($"{variable.Key}: {variable.Value}");
			}
		}


	}
}
