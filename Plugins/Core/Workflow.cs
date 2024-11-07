using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Plugins.Core.Workflows;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Provides functionality to execute a workflow plan by invoking specified methods.
	/// The executor manages variables and handles method invocation using reflection.
	/// </summary>
	public class Workflow
	{

		/// <summary>
		/// Will be used by plugins manager.
		/// </summary>
		/// <summary>
		/// Will be used by plugins manager and called by AI.
		/// </summary>
		public Func<Plan, CancellationToken, Task<OperationResult<bool>>> ExecutePlanCallback { get; set; }

		/// <summary>
		/// Executes the workflow plan by invoking methods defined in the plan.
		/// </summary>
		/// <param name="plan">The workflow plan containing variables and operations to execute.</param>
		/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
		[RiskLevel(RiskLevel.Critical)]
		public async Task<OperationResult<bool>> ExecutePlan(Plan plan, CancellationToken cancellationToken = default)
		{
			try
			{
				var result = await ExecutePlanCallback(plan, cancellationToken);
				return result;
			}
			catch (Exception ex)
			{
				return new OperationResult<bool>(ex);
			}

		}
	}
}
