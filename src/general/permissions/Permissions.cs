using Discord;
using Discord.WebSocket;
using TicketBox;

static class Permissions {
	public static bool requiredUserPermsSettings(SocketSlashCommand command)
	{
		var userPerms = getUserGuildPerms((SocketGuildUser)command.User);
		return (
			userPerms.Administrator
		);
	}

	public static bool requiredUserPermsClosePoll(SocketMessageComponent messageComponent)
	{
		var userPerms = getUserGuildPerms((SocketGuildUser)messageComponent.User);
		return (
			userPerms.Administrator
			|| userPerms.ManageMessages
		);
	}

	public static bool requiredBotPermsDualChoice(ulong guildId, ulong channelId)
	{
		var botPerms = getBotChannelPerms(
			guildId,
			channelId
		);
		return (
			botPerms.SendMessages
			&& botPerms.ViewChannel
			&& botPerms.CreatePublicThreads
			&& botPerms.CreatePrivateThreads
		);
	}

	private static GuildPermissions getUserGuildPerms(SocketGuildUser user)
	{
		return user.GuildPermissions;
	}

	private static ChannelPermissions getBotChannelPerms(ulong guildId, ulong channelId)
	{
		var server = Program.client.GetGuild(guildId);
		var channel = server.GetChannel(channelId);
		var ticketBox = server.GetUser(Program.client.CurrentUser.Id);

		return ticketBox.GetPermissions(channel);
	}
}