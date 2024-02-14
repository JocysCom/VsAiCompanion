namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Helps AI to auto-continue on the task.
	/// </summary>
	public class AutoContinueHelper
	{

		/// <summary>
		/// Use when you can't provide an answer in one response and need to split the answer.
		/// Use after reply when user asks to generate answers with permission to continue.
		/// Continue with the next part of the reply after this function call return "Please continue".
		/// </summary>
		/// <returns>The output message to reply from the user.</returns>
		/// <param name="reserved">Reserved. Send empty string as a value.</param>
		/// <exception cref="System.Exception">Error message explaining why execution failed.</exception>
		public static string AutoContinue(string reserved)
		{
			return "Please continue.";
		}

	}

}
