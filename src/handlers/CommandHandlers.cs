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
		var embedData = new DualChoiceData(0, 0, pollText, false)
			.userAvatar(command.User.GetAvatarUrl())
			.userName(command.User.ToString())
			.pollDate(DateTimeOffset.Now)
			.expiryString(stringExpiryDate);
		var pollEmbedBuilder = DualChoiceFunctions.createEmbed(null, embedData);

		/* --------------------------- Bot Embed Response --------------------------- */
		var message = await DualChoiceFunctions.createMessage(command, pollEmbedBuilder);
		/* ------------------------------- Data Saving ------------------------------ */
		// Get the server's document
		DualChoiceFunctions.saveInitialPoll(Program.discordServersCollection, command, message.Id, pollText, unixExpiryTime);

	}
}