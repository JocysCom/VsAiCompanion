namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Connection security.
	/// </summary>
	public enum MailConnectionSecurity
	{
		/// <summary>None</summary>
		None,
		/// <summary>Allow client to decide.</summary>
		Auto,
		/// <summary>Use SSL or TLS encryption immediately.</summary>
		SslOnConnect,
		/// <summary>Use TLS encryption immediately after reading the greeting and capabilities of the server.</summary>
		StartTls,
		/// <summary>Use TLS encryption immediately after reading the greeting and capabilities of the server, but only if STARTTLS is available.</summary>
		StartTlsWhenAvailable
	}
}
