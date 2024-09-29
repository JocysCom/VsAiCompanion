namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Represents the components of a user prompt in the RISEN Framework.
	/// Each member defines a specific part of the prompt to guide the AI's response.
	/// </summary>
	public enum RisenType
	{
		None = 0,
		/// <summary>
		/// Defines the role or persona the AI should adopt, setting the context for interaction.
		/// </summary>
		Role,
		/// <summary>
		/// Provides specific instructions or directives on what you want the AI to do.
		/// </summary>
		Instructions,
		/// <summary>
		/// Breaks down the task into logical steps to ensure a structured approach.
		/// </summary>
		Steps,
		/// <summary>
		/// Specifies the ultimate objective or desired outcome to guide the AI's focus.
		/// </summary>
		EndGoal,
		/// <summary>
		/// Applies constraints or limitations to tailor the AI's response to specific needs.
		/// </summary>
		Narrowing,
	}
}
