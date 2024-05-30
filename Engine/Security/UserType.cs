using System;

namespace JocysCom.VS.AiCompanion.Engine.Security
{

	/// <summary>
	/// Enumeration representing different types of Microsoft users.
	/// </summary>
	[Flags]
	public enum UserType
	{
		/// <summary>
		/// No user type specified.
		/// </summary>
		None = 0,

		/// <summary>
		/// Local user on a single computer.
		/// </summary>
		Local = 1 << 0,

		/// <summary>
		/// User in a Windows Domain environment.
		/// </summary>
		WindowsDomain = 1 << 1,

		/// <summary>
		/// Microsoft 365 (formerly Office 365) Business user.
		/// </summary>
		MicrosoftBusiness = 1 << 2,

		/// <summary>
		/// Microsoft Consumer user (e.g., personal Microsoft account).
		/// </summary>
		MicrosoftConsumer = 1 << 3,

		/// <summary>
		/// Microsoft Entra ID (formerly Azure AD) user.
		/// </summary>
		EntraID = 1 << 4
	}
}
