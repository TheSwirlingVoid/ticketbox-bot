using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class CommandHandlers {
	public static async Task PollDualChoiceCommand(BsonDocument document, SocketSlashCommand command, BotSettings settings, string pollText) 
	{	

		var expiryDate = DateTimeOffset.Now.Date.AddDays(settings.ExpiryDays);
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
		if (settings.CreateThreads)
			await ((ITextChannel)command.Channel).CreateThreadAsync($"Poll Discussion—\"{pollText}\"", message: message);
		/* ------------------------------- Data Saving ------------------------------ */
		// Get the server's document
		var baseData = new BaseDocumentData(new BaseMessageData(message.Id, pollText), new BaseTimeData(unixExpiryTime));
		DualChoiceFunctions.saveInitialPoll(document, Program.discordServersCollection, command, baseData);

	}

	public static async Task SettingsCommand(DocumentWithCollection docCollection, SocketSlashCommand command, SocketSlashCommandDataOption[] options, GuildPermissions permissions)
	{
		var subCommand = options.First();
		var subCommandName = subCommand.Name;
		var commandParameters = subCommand.Options.ToArray();

		// this will be the validated value of the user input
		BsonValue validated;
		var successfulValidation = CommandFunctions.validateValue(subCommandName, commandParameters[0], out validated);
		if (!successfulValidation)
		{
			var failString = CommandFunctions.getParameterFailString(subCommandName);
			await command.RespondAsync(failString, ephemeral: true);
			return;
		}

		if (permissions.Administrator)
		{
			var setting = subCommandName;
			await CommandHandlers.changeOption(
						docCollection,
						(string)subCommandName, 
						validated
					);
			await command.RespondAsync(
				$"The setting `{subCommandName}` was successfully updated to `{validated.ToString()}`!", 
				ephemeral: true
			);
		}
		else
			await command.RespondAsync("You need to be an **Administrator** to change this bot's server settings!", ephemeral: true);
	}

	public static async Task changeOption(DocumentWithCollection docCollection, String option, BsonValue value)
	{
		var document = docCollection.document;
		var collection = docCollection.collection;

		var updateInstruction = Builders<BsonDocument>.Update.Set($"bot_options.{option}", value);
		await collection.UpdateOneAsync(document, updateInstruction);

	}
}