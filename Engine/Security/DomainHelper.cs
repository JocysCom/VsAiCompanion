using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	/// <summary>
	/// Allows restrict AI risk level opitons with domain groups.
	/// </summary>
	public static class DomainHelper
	{

		private static object _RiskLevelLock = new object();
		private static bool RiskLevelAcquired;
		private static RiskLevel? _UserMaxRiskLevel;

		public static RiskLevel? GetDomainUserMaxRiskLevel(bool cache = true)
		{
			lock (_RiskLevelLock)
			{
				// Getting groups from domain is slow.
				// Restart the app if permissions changed.
				if (cache && RiskLevelAcquired)
					return _UserMaxRiskLevel;
				// If app runs on domain then...
				var isDomainUser = JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser();
				if (isDomainUser)
				{
					// If risk groups found then...
					var domainRiskGroups = GetDomainRiskGroups();
					if (domainRiskGroups.Values.Any(x => x))
						// Get user maximum risk level.
						_UserMaxRiskLevel = GetUserRiskGroups()
							.Where(x => x.Value).Max(x => x.Key);
				}
				RiskLevelAcquired = true;
			}
			return _UserMaxRiskLevel;
		}

		private static Dictionary<RiskLevel, bool> GetLevels()
		{
			var dic = ((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new RiskLevel[] { RiskLevel.Unknown })
				.ToDictionary(x => x, x => false);
			return dic;
		}

		private static string GetGroupName(RiskLevel level)
			=> $"AI_{nameof(RiskLevel)}_{level}";

		/// <summary>
		/// Get risk groups available on domain.
		/// </summary>
		public static Dictionary<RiskLevel, bool> GetDomainRiskGroups()
		{
			var allGroups = JocysCom.ClassLibrary.Security.PermissionHelper.GetAllGroups(
				ContextType.Domain, samAccountName: "AI_RiskLevel_*");
			var dic = GetLevels();
			var levels = dic.Keys.ToArray();
			foreach (var level in levels)
			{
				var groupName = GetGroupName(level);
				var exists = allGroups.Any(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
				dic[level] = exists;
			}
			return dic;
		}

		/// <summary>
		/// Get risk groups available on domain.
		/// </summary>
		public static Dictionary<RiskLevel, bool> GetUserRiskGroups()
		{
			var user = WindowsIdentity.GetCurrent().User;
			var allGroups = GetUserGroupMemberships(user);
			var dic = GetLevels();
			foreach (var level in dic.Keys)
			{
				var groupName = GetGroupName(level);
				var exists = allGroups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
				dic[level] = exists;
			}
			return dic;
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
