using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class CommandHandlers {
	public static async Task SuggestionCommand(SocketSlashCommand command) 
	{	
		
		/* ------------------------------ Embed Builder ----------------------------- */
		var commandOptions = command.Data.Options.ToArray();
		var suggestionText = (string) commandOptions[0].Value;
		var suggestEmbedBuilder = SuggestFunctions.createEmbed(suggestionText, command, null, 0, 0);

		/* --------------------------- Bot Embed Response --------------------------- */
		var message = await SuggestFunctions.createMessage(command, suggestEmbedBuilder);
		/* ------------------------------- Data Saving ------------------------------ */
		// Get the server's document
		SuggestFunctions.saveInitialSuggestion(Program.collection, command.GuildId.GetValueOrDefault(), command, message.Id, suggestionText);

	}
}