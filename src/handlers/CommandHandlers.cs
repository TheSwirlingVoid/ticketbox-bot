using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class CommandHandlers {
	public static async Task PollDualChoiceCommand(SocketSlashCommand command, BotSettings settings, string pollText) 
	{
		var expiryDate = DateTimeOffset.Now.AddDays(settings.ExpiryDays);
		var stringExpiryDate = $"Expires {expiryDate.Date.ToString("MM/dd/yyyy")}";
		var unixExpiryTime = ((DateTimeOffset) expiryDate).ToUnixTimeSeconds();
		/* ------------------------------ Embed Builder ----------------------------- */
		var commandOptions = command.Data.Options.ToArray();

		var embedData = new DualChoiceEmbedData(
			command.User.GetAvatarUrl(),
			command.User.ToString(),
			DateTimeOffset.Now,
			stringExpiryDate
		);


		/* --------------------------- Bot Embed Response --------------------------- */
		var pollEmbed = embedData.createInitialEmbed(pollText);
		var message = await DualChoice.createMessage(command, pollEmbed);

		var coreData = new DualChoiceCoreData(
			pollText,
			new MessageScope(command.GuildId.GetValueOrDefault(),
			command.ChannelId.GetValueOrDefault(),
			message.Id),
			0,
			0
		);
		var dualChoice = new DualChoice(coreData, settings);
		dualChoice.ExpiryTime = unixExpiryTime;
		dualChoice.EmbedData = embedData;

		if (settings.CreateThreads)
			await ((ITextChannel)command.Channel).CreateThreadAsync($"Poll Discussion—\"{pollText}\"", message: message);
		/* ------------------------------- Data Saving ------------------------------ */
		dualChoice.saveInitialPoll(command);
	}

	public static async Task SettingsCommand(DocumentWithCollection docCollection, SocketSlashCommand command, SocketSlashCommandDataOption[] options, GuildPermissions permissions)
	{
		var subCommand = options.First();
		var subCommandName = subCommand.Name;
		var commandParameters = subCommand.Options.ToArray();

		// this will be the validated value of the user input
		/* ---------------------------- Value Validation ---------------------------- */
		BsonValue validated;
		var successfulValidation = CommandFunctions.validateValue(subCommandName, commandParameters[0], out validated);
		if (!successfulValidation)
		{
			var failString = CommandFunctions.getParameterFailString(subCommandName);
			await command.RespondAsync(failString, ephemeral: true);
			return;
		}

		/* ------------------------- Permission Verification ------------------------ */
		if (permissions.Administrator)
		{
			var setting = subCommandName;
			/* ------------------------------ Value Change ------------------------------ */
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