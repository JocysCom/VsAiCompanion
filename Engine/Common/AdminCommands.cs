using JocysCom.ClassLibrary.Configuration;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Some actions require for app to run as Administrator.
	/// In most cases app will run with permissions of normal user.
	/// In order to get around this issue, app will run second copy of itself with 
	/// Administrative permissions.
	/// </summary>
	public class AdminCommands
	{
		/// <summary>
		/// Returns true if command was executed locally.
		/// </summary>
		public static bool RunElevated(AdminCommand command, string param = null)
		{
			// If program is running as Administrator already.
			var argument = command.ToString();
			if (param != null)
			{
				argument = string.Format("{0}=\"{1}\"", command, param);
			}
			if (JocysCom.ClassLibrary.Security.PermissionHelper.IsElevated)
			{
				// Run command directly.
				var args = new string[] { argument };
				ProcessAdminCommands(args);
				return true;
			}
			else
			{
				// Run copy of app as Administrator.
				JocysCom.ClassLibrary.Win32.UacHelper.RunElevated(
					new AssemblyInfo().CodeBase,
					argument,
					System.Diagnostics.ProcessWindowStyle.Hidden
				);
				return false;
			}
		}

		public static bool ProcessAdminCommands(string[] args)
		{
			// Requires System.Configuration.Installl reference.
			//var ic = new System.Configuration.Install.InstallContext(null, args);
			// ------------------------------------------------
			/*
			if (ic.Parameters.ContainsKey(AdminCommand.UpdaterRenameFiles.ToString()))
			{
				// Rename application files during update.
				return true;
			}
			// ------------------------------------------------
			if (ic.Parameters.ContainsKey(AdminCommand.UpdaterRestartApp.ToString()))
			{
				// Restart application during update.
				return true;
			}
			*/
			return false;
		}


	}
}
