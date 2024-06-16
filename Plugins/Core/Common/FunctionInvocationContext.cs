using System.Reflection;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// 
	/// </summary>
	public class FunctionInvocationContext
	{
		/// <summary>
		/// Context and rationale provided by the AI when invoking a function.
		/// </summary>
		/// <param name="reasonForInvocation">
		/// A detailed explanation of the purpose or context behind calling this function. 
		/// This should be a concise justification, up to 3 sentences,
		/// describing why this function call is necessary in the given situation.
		/// </param>
		static void MethodForContext(string reasonForInvocation) { }

		/// <summary>
		/// Get method info
		/// </summary>
		public static MethodInfo ContextMethodInfo
			=> _ContextMethodInfo = _ContextMethodInfo ?? typeof(FunctionInvocationContext).GetMethod(nameof(MethodForContext), BindingFlags.Static | BindingFlags.NonPublic);
		static MethodInfo _ContextMethodInfo;

		/// <summary>
		/// Get parameter info
		/// </summary>
		public static ParameterInfo[] ContextParameterInfos
			=> _ContextParameterInfo = _ContextParameterInfo ?? ContextMethodInfo.GetParameters();
		static ParameterInfo[] _ContextParameterInfo;

	}
}
