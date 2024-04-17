using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Allows restrict AI risk level opitons with domain groups.
	/// </summary>
	public static class DomainHelper
	{

		/// <summary>
		/// Determines whether the application is running under a domain user account.
		/// </summary>
		/// <returns>True if the application is running under a domain user account; otherwise, false.</returns>
		public static bool IsApplicationRunningOnDomain()
		{
			bool isDomainUser = JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser();
			return isDomainUser;
		}

		/// <summary>
		/// Get risk groups.
		/// </summary>
		public static string[] GetDomainRiskGroups()
		{
			var riskGroups = new List<string>();
			var allGroups = JocysCom.ClassLibrary.Security.PermissionHelper.GetAllGroups(System.DirectoryServices.AccountManagement.ContextType.Domain);
			var specificGroups = ((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Select(x => $"AI_{nameof(RiskLevel)}_{x}").ToList();
			foreach (var groupName in specificGroups)
			{
				var exists = allGroups.Any(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
				if (exists)
					riskGroups.Add(groupName);
			}
			return riskGroups.Distinct().ToArray();
		}

		/// <summary>
		/// Checks if specified domain groups are available in the Active Directory.
		/// </summary>
		/// <param name="groupNames">An array of domain group names to check for existence.</param>
		/// <returns>A dictionary where the key is the group name and the value indicates whether the group exists.</returns>
		public static Dictionary<string, bool> AreDomainGroupsAvailable(string[] groupNames)
		{
			var allGroups = ClassLibrary.Security.PermissionHelper.GetAllGroups(ContextType.Domain);
			var groupExistenceMap = new Dictionary<string, bool>();
			foreach (var groupName in groupNames)
			{
				var exists = allGroups.Any(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
				groupExistenceMap[groupName] = exists;
			}
			return groupExistenceMap;
		}

		/// <summary>
		/// Retrieves the group memberships of a user, both direct and indirect.
		/// </summary>
		/// <param name="userSid">The Security Identifier (SID) of the user.</param>
		/// <param name="includeGroups">Filter to only include groups specified; if null, all groups are included.</param>
		/// <returns>A list of names of the groups the user is a member of.</returns>
		public static List<string> GetUserGroupMemberships(SecurityIdentifier userSid, string[] includeGroups = null)
		{
			var userGroups = JocysCom.ClassLibrary.Security.PermissionHelper.GetUserGroups(userSid);
			if (includeGroups is null || !includeGroups.Any())
			{
				return userGroups.Select(g => g.Name).ToList();
			}
			else
			{
				return userGroups
					.Where(g => includeGroups.Contains(g.Name))
					.Select(g => g.Name).ToList();
			}
		}

	}
}
