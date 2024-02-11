using Microsoft.AspNetCore.Mvc;
using System;

namespace JocysCom.VS.AiCompanion.Plugins.PowerShellExecutor.Controllers
{
	/// <summary>
	/// Execute PowerShell scripts and return the results.
	/// Allows AI to execute PowerShell scripts on the local machine.
	/// Use this API with caution due to security risks.
	/// </summary>
	[ApiController]
	[Route("[controller]")]
	public class PowerShellExecutorController : ControllerBase
	{
		/// <summary>
		/// Execute a PowerShell script
		/// </summary>
		/// <param name="script">The PowerShell script to be executed.</param>
		/// <returns>The output of the executed PowerShell script.</returns>
		/// <exception cref="System.Exception">Error message explaining why the request failed.</exception>
		/// <remarks>Be cautious with executing scripts received via API due to security risks.</remarks>
		[HttpPost]
		[Route("execute")]
		public ActionResult<string> ExecuteScript(string script)
		{
			if (string.IsNullOrWhiteSpace(script))
			{
				return BadRequest("Script is required.");
			}
			try
			{
				string output = Core.PowerShellExecutorHelper.RunPowerShellScript(script);
				return Ok(output);
			}
			catch (Exception ex)
			{
				// Log the exception as needed
				return StatusCode(500, $"An error occurred while executing the script: {ex.Message}");
			}
		}

	}
}
