using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Specifies the risk level of a method or a class.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
	public class RiskLevelAttribute : Attribute
	{
		/// <summary>
		/// Gets the risk level assigned to the method or class.
		/// </summary>
		public RiskLevel Level { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RiskLevelAttribute"/> class with a specified risk level.
		/// </summary>
		/// <param name="level">The risk level to be assigned.</param>
		public RiskLevelAttribute(RiskLevel level)
		{
			Level = level;
		}
	}
}
