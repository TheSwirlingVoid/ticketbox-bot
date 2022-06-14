using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class CommandHandlers {
	public static async Task PollDualChoiceCommand(SocketSlashCommand command, string pollText) 
	{	
		var expiryDate = DateTimeOffset.Now.Date.AddDays(7);
		var stringExpiryDate = $"Expires {expiryDate.Date.ToString("MM/dd/yyyy")}";
		var unixExpiryTime = ((DateTimeOffset) expiryDate).ToUnixTimeSeconds();
		/* ------------------------------ Embed Builder ----------------------------- */
		var commandOptions = command.Data.Options.ToArray();
		var embedData = new DualChoiceData()
					.upvotes(0)
					.downvotes(0)
					.pollText(pollText)
					.userAvatar(command.User.GetAvatarUrl())
					.userName(command.User.ToString())
					.pollDate(DateTimeOffset.Now)
					.expiryDate(stringExpiryDate)
					.closedVoting(false);
		var pollEmbedBuilder = DualChoiceFunctions.createEmbed(command, null, embedData);

		/* --------------------------- Bot Embed Response --------------------------- */
		var message = await DualChoiceFunctions.createMessage(command, pollEmbedBuilder);
		/* ------------------------------- Data Saving ------------------------------ */
		// Get the server's document
		DualChoiceFunctions.saveInitialPoll(Program.discordServersCollection, command.GuildId.GetValueOrDefault(), command, message.Id, pollText, unixExpiryTime);

	}
}