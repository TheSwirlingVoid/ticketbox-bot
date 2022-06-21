using Discord;

class CommandList {
	private List<ApplicationCommandProperties> commands = new();

	public CommandList()
	{
		// POLL COMMAND
		var poll_command = new SlashCommandBuilder()
			.WithName(BotCommands.POLL)
			.WithDescription("Create a poll!")
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("dualchoice")
				.WithDescription("Create a poll with upvote and downvote vote choices!")
				.WithType(ApplicationCommandOptionType.SubCommand)
				.AddOption(new SlashCommandOptionBuilder()
					.WithName("query")
					.WithDescription("Enter your question, suggestion, or query!")
					.WithType(ApplicationCommandOptionType.String)
					.WithRequired(true)
				)
			)
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("dropdown")
				.WithDescription("Create a dropdown poll with custom vote choices!")
				.WithType(ApplicationCommandOptionType.SubCommand)
				.AddOption(new SlashCommandOptionBuilder()
					.WithName("options")
					.WithDescription("Write the corresponding options in order! ex. \"Coffee, Champagne\"")
					.WithType(ApplicationCommandOptionType.String)
					.WithRequired(true)
				)
			).Build();

		commands.Add(poll_command);

		var settings_command = new SlashCommandBuilder()
			.WithName(BotCommands.SETTINGS)
			.WithDescription("Change the bot's settings for this server!")
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("expiry_days")
				.WithDescription("Change the amount of days for a poll to expire!")
				.WithType(ApplicationCommandOptionType.SubCommand)
				.AddOption(new SlashCommandOptionBuilder()
					.WithName("days")
					.WithDescription("Days before a poll expires")
					.WithType(ApplicationCommandOptionType.Integer)
					.WithRequired(true)
				)
			)
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("create_threads")
				.WithDescription("Change whether poll discussion threads should be created!")
				.WithType(ApplicationCommandOptionType.SubCommand)
				.AddOption(new SlashCommandOptionBuilder()
					.WithName("threads_enabled")
					.WithDescription("Threads enabled")
					.WithType(ApplicationCommandOptionType.Boolean)
					.WithRequired(true)
				)
			)
			.Build();
		commands.Add(settings_command);
	}

	public List<ApplicationCommandProperties> getCommands()
	{
		return commands;
	}
}