using System;

namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public enum audit_log_event_type
	{
		api_keycreated,
		api_keyupdated,
		api_keydeleted,
		invitesent,
		inviteaccepted,
		invitedeleted,
		loginsucceeded,
		loginfailed,
		logoutsucceeded,
		logoutfailed,
		organizationupdated,
		projectcreated,
		projectupdated,
		projectarchived,
		service_accountcreated,
		service_accountupdated,
		service_accountdeleted,
		rate_limitupdated,
		rate_limitdeleted,
		useradded,
		userupdated,
		userdeleted,
	}
}
