using Discord.WebSocket;

static class Permissions {
	public static bool userCanClosePoll(SocketGuildUser user)
	{
		var permissions = user.GuildPermissions;
		return (permissions.ManageMessages || permissions.Administrator);
	}
	public static bool userCanChangeSettings(SocketGuildUser user)
	{
		var permissions = user.GuildPermissions;
		return (permissions.Administrator);
	}
}