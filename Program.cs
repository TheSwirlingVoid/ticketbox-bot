using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TicketBox
{
	class Program
	{
		
		public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

		public static DiscordSocketClient client = new DiscordSocketClient();
		private static readonly string connectionString = "mongodb://localhost:27017/";
		public static MongoClient mongoClient = new MongoClient(connectionString);
		public static IMongoDatabase database = mongoClient.GetDatabase("ticketbox-bot");
		public static IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("DiscordServers");
		
		public async Task MainAsync()
		{
			client.Log += Log; // add the log function to handle log events
			client.Ready += client_ready;
			client.JoinedGuild += JoinedGuild;

			client.SlashCommandExecuted += SlashCommandHandler;
			client.ButtonExecuted += ButtonExecuted;

			//* DO NOT PLACE TOKEN HERE FOR SECURITY W/ PUBLIC REPOSITORIES
			string token = File.ReadAllText("token.txt");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

			// Wait until program is closed
			await Task.Delay(-1);
		}

		private Task Log(LogMessage msg) 
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		public static FilterDefinition<BsonDocument> serverIDFilter(ulong guildId)
		{
			return Builders<BsonDocument>.Filter.Eq("server_id", guildId);
		}

		private async Task client_ready()
		{
			var guild = client.GetGuild(837935655258554388);

			/* -------------------------------- Commands -------------------------------- */
			List<ApplicationCommandProperties> commands = new();

			// SUGGEST COMMAND
			var suggestCommand = new SlashCommandBuilder()
				.WithName(BotCommands.SUGGESTCOMMAND)
				.WithDescription("Make a new suggestion for the server!")
				.AddOption("suggestion", ApplicationCommandOptionType.String, "Enter your suggestion!", true);
			commands.Add(suggestCommand.Build());

			/* -------------------------- Commmand Registering -------------------------- */
			try 
			{	
				// SLASH COMMAND CREATION
				await guild.BulkOverwriteApplicationCommandAsync(commands.ToArray());

				//* CLEAR GLOBAL CMDS FOR NOW, TRANSFER LATER
				await client.BulkOverwriteGlobalApplicationCommandsAsync(new ApplicationCommandProperties[0]);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
			/* -------------------------------------------------------------------------- */

			await client.SetStatusAsync(UserStatus.Online);
			await client.SetGameAsync("around in my testing phase. I don't bite!", null, ActivityType.Playing);
		}

		private async Task SlashCommandHandler(SocketSlashCommand command)
		{
			switch(command.CommandName)
			{
				case "suggest":
				await CommandHandlers.SuggestionCommand(command);
				break;
			}
		}

		private async Task JoinedGuild(SocketGuild guild)
		{
			// If this server does not have its own document already
			if (collection.Find(serverIDFilter(guild.Id)).CountDocuments() == 0)
			{
				// Create the base BSON Document
				BsonDocument newServerDocument = new BsonDocument
				{
					{ "server_id", BsonValue.Create(guild.Id) },
					{ "server_name", guild.Name },
					{ "bot_options", 
						new BsonDocument { 
							// {"show_votes", true} 
						}
					},
					{ "current_suggestions", new BsonArray {} }
				};
				await collection.InsertOneAsync(newServerDocument);
			}
		}

		private async Task ButtonExecuted(SocketMessageComponent messageComponent)
		{
			switch (messageComponent.Data.CustomId)
			{
				case "upvote-suggestion" or "downvote-suggestion":
				try
				{
					await ButtonHandlers.VoteButton(messageComponent, messageComponent.Data.CustomId);
				}
				catch(Discord.Net.HttpException e)
				{
					if (e.DiscordCode != DiscordErrorCode.CannotSendEmptyMessage)
					{
						// code for discarding "cannot send an empty message" error
					}
				}
				break;
			}
		}
	}
}