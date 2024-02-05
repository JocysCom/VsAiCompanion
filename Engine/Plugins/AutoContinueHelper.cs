namespace JocysCom.VS.AiCompanion.Engine.Plugins
{

	public class AutoContinueHelper
	{

		/// <summary>
		/// Use it to ask user for permission to continue working on the task.
		/// </summary>
		/// <returns>The output message to reply from the user.</returns>
		/// <exception cref="System.Exception">Error message explaining why execution failed.</exception>
		/// <remarks>Be cautious with executing scripts received via API due to security risks.</remarks>
		public static string AutoContinue(string message)
		{
			return "please continue";
		}

	}

}
